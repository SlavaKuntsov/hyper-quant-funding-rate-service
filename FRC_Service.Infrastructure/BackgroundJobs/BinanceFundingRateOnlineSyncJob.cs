using FRC_Service.Domain.Enums;
using FRC_Service.Domain.Repositories;
using FRC_Service.Infrastructure.Exceptions;
using Microsoft.Extensions.Logging;
using Quartz;
using UltimaBinanceConnector.Enums;
using UltimaBinanceConnector.Interfaces.Clients;
using UltimaBinanceConnector.Objects.Models.Futures;

namespace FRC_Service.Infrastructure.BackgroundJobs;

[DisallowConcurrentExecution]
public class BinanceFundingRateOnlineSyncJob(
    ILogger<BinanceFundingRateOnlineSyncJob> logger,
    IBinanceRestClient binanceClient,
    IExchangeRepository exchangeRepository,
    IFundingRateOnlineRepository fundingRateOnlineRepository,
    IUnitOfWork unitOfWork)
    : BaseFundingRateOnlineSyncJob<BinanceFundingRateOnlineSyncJob>(
       logger, exchangeRepository, fundingRateOnlineRepository, unitOfWork)
{
    protected override ExchangeCodeType ExchangeCode => ExchangeCodeType.Binance;
    protected override int MaxNumbersOfParallelism => 5;
    
    /// <inheritdoc/>
    protected override async Task<IEnumerable<object>> FetchSymbolsAsync(CancellationToken cancellationToken = default)
    {
       // Get exchange info to filter only active perpetual contracts
       var exchangeResponse = await binanceClient
          .UsdFuturesApi
          .ExchangeData
          .GetExchangeInfoAsync(cancellationToken);

       if (!exchangeResponse.Success || exchangeResponse.Data == null)
       {
          logger.LogError("Failed to fetch exchange info from Binance: {Error}", 
             exchangeResponse.Error?.Message ?? "Unknown error");
          throw new ExchangeApiException("Binance", null, "Failed to fetch exchange info");
       }

       // Filter for active perpetual contracts only
       var exchangeSymbols = exchangeResponse.Data.Symbols
          .Distinct()
          .Where(s => s.Status == SymbolStatus.Trading && s.ContractType == ContractType.Perpetual);

       logger.LogInformation("Found {Count} active perpetual symbols with funding rates", 
          exchangeSymbols.Count());

       return exchangeSymbols;
    }

    /// <inheritdoc/>
    protected override async Task<(decimal FundingRate, DateTime FundingTime, int? FundingIntervalHours)?> FetchFuturesAsync(string symbolName, CancellationToken cancellationToken = default)
    {
       // Fetch only the latest funding rate
       var response = await binanceClient
          .UsdFuturesApi
          .ExchangeData
          .GetFundingRatesAsync(symbolName, limit: 1, ct: cancellationToken);

       if (!response.Success)
       {
          throw new ExchangeApiException("Binance", null, $"Failed to fetch funding rate for {symbolName}");
       }

       // Check if data is available
       if (response.Data == null || !response.Data.Any())
       {
          logger.LogWarning("No funding rate data available for {Symbol}", symbolName);
          return null;
       }
    
       var data = response.Data.First();

       return (data.FundingRate, data.FundingTime, (int?)null);
    }
    
    /// <inheritdoc/>
    protected override string GetSymbolName(object symbol)
    {
       return ((BinanceFuturesUsdtSymbol)symbol).Name;
    }
}