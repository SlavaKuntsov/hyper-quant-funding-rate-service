using FluentValidation;
using FRC_Service.Application.Abstractions;
using FRC_Service.Application.Services;
using Microsoft.Extensions.DependencyInjection;

namespace FRC_Service.Application.Extensions;

/// <summary>
/// Provides extension methods for <see cref="IServiceCollection"/> to configure Application project.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers application services including services, mapping and validation.
    /// </summary>
    /// <param name="services">The service collection</param>
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        // Register history services
        services.AddScoped<BinanceFundingRateHistoryService>();
        services.AddScoped<BybitFundingRateHistoryService>();
        services.AddScoped<HyperLiquidFundingRateHistoryService>();
        services.AddScoped<MexcFundingRateHistoryService>();

        services.AddScoped<IFundingRateHistoryService>(provider => 
            provider.GetRequiredService<BinanceFundingRateHistoryService>());

        services.AddScoped<IFundingRateHistoryService>(provider => 
            provider.GetRequiredService<BybitFundingRateHistoryService>());

        services.AddScoped<IFundingRateHistoryService>(provider => 
            provider.GetRequiredService<HyperLiquidFundingRateHistoryService>());

        services.AddScoped<IFundingRateHistoryService>(provider => 
            provider.GetRequiredService<MexcFundingRateHistoryService>());

        // Register online services
        services.AddScoped<BinanceFundingRateOnlineService>();
        services.AddScoped<BybitFundingRateOnlineService>();
        services.AddScoped<HyperLiquidFundingRateOnlineService>();
        services.AddScoped<MexcFundingRateOnlineService>();

        services.AddScoped<IFundingRateOnlineService>(provider => 
            provider.GetRequiredService<BinanceFundingRateOnlineService>());

        services.AddScoped<IFundingRateOnlineService>(provider => 
            provider.GetRequiredService<BybitFundingRateOnlineService>());

        services.AddScoped<IFundingRateOnlineService>(provider => 
            provider.GetRequiredService<HyperLiquidFundingRateOnlineService>());

        services.AddScoped<IFundingRateOnlineService>(provider => 
            provider.GetRequiredService<MexcFundingRateOnlineService>());
        
        // Configures validation.  
        services.AddValidatorsFromAssembly(AssemblyReference.Assembly);
        services.AddProblemDetails();
        
        return services;
    }
}