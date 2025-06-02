using FRC_Service.Domain.Enums;
using FRC_Service.Domain.Repositories;
using FRC_Service.Infrastructure.Exceptions;
using Microsoft.Extensions.Logging;
using Quartz;
using UltimaBybitConnector.Enums;
using UltimaBybitConnector.Interfaces.Clients;
using UltimaBybitConnector.Objects.Models.V5;

namespace FRC_Service.Infrastructure.BackgroundJobs;

[DisallowConcurrentExecution]
public class BybitFundingRateOnlineSyncJob(
    ILogger<BybitFundingRateOnlineSyncJob> logger,
    IBybitRestClient bybitClient,
    IExchangeRepository exchangeRepository,
    IFundingRateOnlineRepository fundingRateOnlineRepository,
    IUnitOfWork unitOfWork)
    : BaseFundingRateOnlineSyncJob<BybitFundingRateOnlineSyncJob>(
       logger, exchangeRepository, fundingRateOnlineRepository, unitOfWork)
{
    protected override ExchangeCodeType ExchangeCode => ExchangeCodeType.Bybit;
    protected override int MaxNumbersOfParallelism => 10;
    
    /// <inheritdoc/>
    protected override async Task<IEnumerable<object>> FetchSymbolsAsync(CancellationToken cancellationToken = default)
    {
       // Get linear inverse symbols for perpetual contracts
       var response = await bybitClient
          .V5Api
          .ExchangeData
          .GetLinearInverseSymbolsAsync(
             Category.Linear, 
             limit: 1000, 
             ct: cancellationToken);

       if (!response.Success || response.Data?.List == null)
       {
          logger.LogError("Failed to fetch linear inverse symbols from Bybit: {Error}", 
             response.Error?.Message ?? "Unknown error");
          throw new ExchangeApiException("Bybit", null, "Failed to fetch symbols");
       }

       // Filter for active linear perpetual contracts only
       var symbols = response.Data.List
          .Distinct()
          .Where(s => s.Status == SymbolStatus.Trading && s.ContractType == ContractTypeV5.LinearPerpetual)
          .ToList();
    
       logger.LogInformation("Found {Count} active linear perpetual symbols", symbols.Count);
       
       return symbols;
    }

    /// <inheritdoc/>
    protected override async Task<(decimal FundingRate, DateTime FundingTime, int? FundingIntervalHours)?> FetchFuturesAsync(string symbolName, CancellationToken cancellationToken = default)
    {
       // Fetch only the latest funding rate
       var response = await bybitClient
          .V5Api
          .ExchangeData
          .GetFundingRateHistoryAsync(
             Category.Linear,
             symbolName,
             limit: 1,
             ct: cancellationToken);

       if (!response.Success)
       {
          logger.LogError("Failed to fetch funding rate for {Symbol}: {Error}", 
             symbolName, response.Error?.Message ?? "Unknown error");
          throw new ExchangeApiException("Bybit", null, $"Failed to fetch funding rate for {symbolName}");
       }

       // Check if data is available
       if (response.Data?.List == null || !response.Data.List.Any())
       {
          logger.LogWarning("No funding rate data available for {Symbol}", symbolName);
          return null;
       }

       var data = response.Data.List.First();

       return (data.FundingRate, data.Timestamp, (int?)null);
    }
    
    /// <inheritdoc/>
    protected override string GetSymbolName(object symbol)
    {
       return ((BybitLinearInverseSymbol)symbol).Name;
    }
}