using FRC_Service.Domain.Enums;
using FRC_Service.Domain.Models;
using FRC_Service.Domain.Repositories;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace FRC_Service.Infrastructure.Extensions;

/// <summary>
/// Provides extension methods for seeding exchanges into the database
/// </summary>
public static class ExchangeSeedExtensions
{
    /// <summary>
    /// Seeds the database with supported exchanges if they do not already exist
    /// </summary>
    /// <param name="app">The application builder</param>
    /// <returns>The application builder for chaining</returns>
    public static async Task SeedExchangesAsync(this IApplicationBuilder app)
    {
        using var scope = app.ApplicationServices.CreateScope();
        var services = scope.ServiceProvider;
        var logger = services.GetRequiredService<ILogger<IApplicationBuilder>>();
        var exchangeRepository = services.GetRequiredService<IExchangeRepository>();
        var unitOfWork = services.GetRequiredService<IUnitOfWork>();

        try
        {
            var supportedExchangeCodes = new[]
            {
                ExchangeCodeType.Binance,
                ExchangeCodeType.Bybit,
                ExchangeCodeType.HyperLiquid,
                ExchangeCodeType.Mexc
            };

            await SeedExchangesAsync(exchangeRepository, unitOfWork, supportedExchangeCodes, logger);
            logger.LogInformation("Exchanges seeded successfully.");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "An error occurred while seeding exchanges");
        }
    }

    private static async Task SeedExchangesAsync(
        IExchangeRepository exchangeRepository,
        IUnitOfWork unitOfWork,
        IEnumerable<ExchangeCodeType> exchangeNames,
        ILogger logger)
    {
        foreach (var exchangeCode in exchangeNames)
        {
            var existingExchange = await exchangeRepository.GetByCodeAsync(exchangeCode);
            if (existingExchange == null)
            {
                var exchange = new Exchange
                {
                    Id = Guid.NewGuid(),
                    Code = exchangeCode
                };

                await exchangeRepository.AddAsync(exchange);
                logger.LogInformation("Added exchange: {ExchangeName}", exchangeCode);
            }
            else
            {
                logger.LogInformation("Exchange already exists: {ExchangeName}", exchangeCode);
            }
        }

        await unitOfWork.SaveAsync();
    }
}