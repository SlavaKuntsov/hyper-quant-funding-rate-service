using FRC_Service.Domain.Enums;
using FRC_Service.Domain.Repositories;
using FRC_Service.Infrastructure.Exceptions;
using Microsoft.Extensions.Logging;
using Quartz;
using UltimaMexcConnector.Interfaces.Clients;
using UltimaMexcConnector.Objects.Models.Futures;

namespace FRC_Service.Infrastructure.BackgroundJobs;

[DisallowConcurrentExecution]
public class MexcFundingRateOnlineSyncJob(
    ILogger<MexcFundingRateOnlineSyncJob> logger,
    IMexcRestClient mexcClient,
    IExchangeRepository exchangeRepository,
    IFundingRateOnlineRepository fundingRateOnlineRepository,
    IUnitOfWork unitOfWork)
    : BaseFundingRateOnlineSyncJob<MexcFundingRateOnlineSyncJob>(
       logger, exchangeRepository, fundingRateOnlineRepository, unitOfWork)
{
    protected override ExchangeCodeType ExchangeCode => ExchangeCodeType.Mexc;
    protected override int MaxNumbersOfParallelism => 2;
    
    /// <inheritdoc/>
    protected override async Task<IEnumerable<object>> FetchSymbolsAsync(CancellationToken cancellationToken = default)
    {
       // Get contract details to retrieve all available futures symbols
       var response = await mexcClient
          .FuturesApi
          .ExchangeData
          .GetContractDetailsAsync(ct: cancellationToken);

       if (!response.Success || response.Data?.Symbols == null)
       {
          logger.LogError("Failed to fetch contract details from MEXC: {Error}", 
             response.Error?.Message ?? "Unknown error");
          throw new ExchangeApiException("MEXC", null, "Failed to fetch contract details");
       }

       // Filter distinct symbols from contract details
       var symbols = response.Data.Symbols.Distinct().ToList();

       logger.LogInformation("Found {Count} futures contract symbols", symbols.Count);
    
       return symbols;
    }

    /// <inheritdoc/>
    protected override async Task<(decimal FundingRate, DateTime FundingTime, int? FundingIntervalHours)?> FetchFuturesAsync(string symbolName, CancellationToken cancellationToken = default)
    {
       // Fetch current funding rate for the specified symbol
       var response = await mexcClient
          .FuturesApi
          .ExchangeData
          .GetFundingRateAsync(symbolName, ct: cancellationToken);

       if (!response.Success)
       {
          logger.LogError("Failed to fetch funding rate for {Symbol}: {Error}", 
             symbolName, response.Error?.Message ?? "Unknown error");
          throw new ExchangeApiException("MEXC", null, $"Failed to fetch funding rate for {symbolName}");
       }

       // Check if data is available
       if (response.Data == null)
       {
          logger.LogWarning("No funding rate data available for {Symbol}", symbolName);
          return null;
       }

       return (response.Data.FundingRate, response.Data.Timestamp, response.Data.CollectCycle);
    }
    
    /// <inheritdoc/>
    protected override string GetSymbolName(object symbol)
    {
       return ((MexcFuturesSymbol)symbol).Name;
    }
}