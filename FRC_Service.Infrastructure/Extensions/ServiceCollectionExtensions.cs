using FRC_Service.Domain.Repositories;
using FRC_Service.Infrastructure.BackgroundJobs;
using FRC_Service.Infrastructure.DataAccess;
using FRC_Service.Infrastructure.DataAccess.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Quartz;

namespace FRC_Service.Infrastructure.Extensions;

/// <summary>
/// Provides extension methods for <see cref="IServiceCollection"/> to configure Infrastructure project.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers infrastructure services including database context and repositories.
    /// </summary>
    /// <param name="services">The service collection</param>
    /// <param name="configuration">The application configuration</param>
    /// <param name="environment">The hosting environment</param>
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment environment)
    {
        services.AddDbContext<ApplicationDbContext>(options =>
        {
            options.UseNpgsql(
                    configuration.GetConnectionString("DefaultConnection"),
                    b => b.MigrationsAssembly(AssemblyReference.Assembly))
                .UseSnakeCaseNamingConvention();

            // Includes more logging info for debugging purposes.
            if (environment.IsDevelopment())
            {
                options
                    .EnableSensitiveDataLogging()
                    .EnableDetailedErrors();
            }
        });

        // Register repositories & unit of work implementations.
        services.AddScoped<IExchangeRepository, ExchangeRepository>();
        services.AddScoped<IFundingRateHistoryRepository, FundingRateHistoryRepository>();
        services.AddScoped<IFundingRateOnlineRepository, FundingRateOnlineRepository>();
        services.AddScoped<IUnitOfWork, UnitOfWork>();

        services.ConfigureQuarts(configuration);
        
        // Registers Binance, Bybit, HyperLiquid and Mexc rest and websocket clients to access exchange information.
        services.AddBinance();
        services.AddBybit();
        services.AddHyperLiquid();
        services.AddMexc();
            
        return services;
    }

    private static void ConfigureQuarts(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddQuartz(options =>
        {
            // Configures PostgreSQL as persistent jobs storage.
            options.UsePersistentStore(configure =>
            {
                configure.UsePostgres(configuration.GetConnectionString("DefaultConnection")!);
                configure.UseNewtonsoftJsonSerializer();
            });

            var historySchedule = configuration.GetValue<string>("BackgroundJobs:FundingRateHistorySyncJobSchedule")!;
            var onlineSchedule = configuration.GetValue<string>("BackgroundJobs:FundingRateOnlineSyncJobSchedule")!;

            // Register history sync jobs for all exchanges
            RegisterSymbolSyncJob<BinanceFundingRateHistorySyncJob>(options, configuration, historySchedule);
            RegisterSymbolSyncJob<BybitFundingRateHistorySyncJob>(options, configuration, historySchedule);
            RegisterSymbolSyncJob<HyperLiquidFundingRateHistorySyncJob>(options, configuration, historySchedule);
            RegisterSymbolSyncJob<MexcFundingRateHistorySyncJob>(options, configuration, historySchedule);

            // Register online sync jobs for all exchanges
            RegisterSymbolSyncJob<BinanceFundingRateOnlineSyncJob>(options, configuration, onlineSchedule);
            RegisterSymbolSyncJob<BybitFundingRateOnlineSyncJob>(options, configuration, onlineSchedule);
            RegisterSymbolSyncJob<HyperLiquidFundingRateOnlineSyncJob>(options, configuration, onlineSchedule);
            RegisterSymbolSyncJob<MexcFundingRateOnlineSyncJob>(options, configuration, onlineSchedule);
        });

        services.AddQuartzHostedService(options => 
        {
            options.WaitForJobsToComplete = true;
        });
    }

    /// <summary>
    /// Registers a symbol synchronization job with its trigger in Quartz.
    /// </summary>
    /// <typeparam name="TJob">Type of the job to register</typeparam>
    /// <param name="options">Quartz configuration options</param>
    /// <param name="configuration">Application configuration</param>
    /// <param name="schedule">Cron schedule</param>
    private static void RegisterSymbolSyncJob<TJob>(
        IServiceCollectionQuartzConfigurator options, 
        IConfiguration configuration, 
        string schedule) where TJob : IJob
    {
        var jobKey = JobKey.Create(typeof(TJob).Name);
        
        options
            .AddJob<TJob>(jobBuilder => jobBuilder
                .WithIdentity(jobKey)
                .DisallowConcurrentExecution()
                .RequestRecovery()
            )
            .AddTrigger(triggerBuilder => triggerBuilder
                .ForJob(jobKey)
                .WithIdentity($"{jobKey.Name}-Trigger")
                .WithCronSchedule(schedule, cronBuilder => 
                    cronBuilder.WithMisfireHandlingInstructionFireAndProceed()
                )
            );
    }
}