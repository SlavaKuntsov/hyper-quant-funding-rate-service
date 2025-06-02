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
public class BybitFundingRateHistorySyncJob(
	ILogger<BybitFundingRateHistorySyncJob> logger,
	IBybitRestClient bybitClient,
	IExchangeRepository exchangeRepository,
	IFundingRateHistoryRepository fundingRateHistoryRepository,
	IUnitOfWork unitOfWork)
	: BaseFundingRateHistorySyncJob<BybitFundingRateHistorySyncJob>(
		logger,
		exchangeRepository,
		fundingRateHistoryRepository,
		unitOfWork)
{
	protected override ExchangeCodeType ExchangeCode => ExchangeCodeType.Bybit;
	protected override int MaxNumbersOfParallelism => 10;
	protected override int BatchSizeForHistory => 50;
	protected override int Limit => 200;

	/// <inheritdoc/>
	protected override async Task<IEnumerable<(object? ExchangeSymbol, object? FundingSymbol)>> FetchSymbolsAsync(CancellationToken cancellationToken = default)
	{	
		// Get linear inverse symbols for perpetual contracts
		var fundingResponse = await bybitClient
			.V5Api
			.ExchangeData
			.GetLinearInverseSymbolsAsync(
				Category.Linear, 
				limit: 1000, 
				ct: cancellationToken);

		if (!fundingResponse.Success)
		{
			throw new ExchangeApiException("Bybit", null, "Failed to fetch funding info");
		}

		// Filter for linear perpetual contracts only
		var fundingSymbols = fundingResponse.Data.List
			.Where(s => s.ContractType == ContractTypeV5.LinearPerpetual)
			.Distinct()
			.Select(f => (ExchangeSymbol: (object?)null, FundingSymbol: (object?)f))
			.ToList();

		logger.LogInformation("Found {Count} linear perpetual symbols with funding rates", fundingSymbols.Count);

		return fundingSymbols;
	}
	
	/// <inheritdoc/>
	protected override async Task<IEnumerable<(decimal FundingRate, DateTime FundingTime, int? FundingIntervalHours)>> FetchAllFundingRatesAsync(
	    string symbolName,
	    DateTime? startTime,
	    CancellationToken cancellationToken = default)
	{
	    var allFundingRateData = new List<(decimal FundingRate, DateTime FundingTime, int? FundingIntervalHours)>();
	    var hasMoreData = true;
	    DateTime? endTime = null;

		// Paginate backwards through historical data until we reach the start time
	    while (hasMoreData && !cancellationToken.IsCancellationRequested)
	    {
	        var response = await bybitClient
	            .V5Api
	            .ExchangeData
	            .GetFundingRateHistoryAsync(
	                Category.Linear,
	                symbolName,
	                endTime: endTime,
	                limit: Limit,
	                ct: cancellationToken);
			
	        if (!response.Success)
	        {
				logger.LogWarning("Failed to fetch funding rates for {Symbol} ending at {EndTime}", 
					symbolName, endTime);
	            break;
	        }
	            
			// Check if we've reached the end of available data
	        if (!response.Data.List.Any())
	        {
	            hasMoreData = false;
	            continue;
	        }
	            
			// Filter items that are within our desired time range
	        var validItems = response.Data.List
	            .Where(f => f.Timestamp >= startTime)
	            .ToList();
	            
			// Convert Bybit response to our internal tuple format
	        allFundingRateData.AddRange(validItems.Select(f => 
	            (f.FundingRate, f.Timestamp, (int?)null)));
	            
			// Find the earliest timestamp in current batch
	        var earliestDate = response.Data.List.Min(f => f.Timestamp);
	            
			// If we've reached data older than our start time, we're done
	        if (earliestDate <= startTime)
	        {
	            hasMoreData = false;
	            continue;
	        }
	            
			// If we received less than the limit, we've reached the end
	        if (response.Data.List.Count() < Limit)
	        {
	            hasMoreData = false;
	            continue;
	        }
	            
			// Prepare for next page - end time is one millisecond before the earliest item
	        endTime = earliestDate.AddMilliseconds(-1);
	    }

		logger.LogDebug("Fetched {Count} historical funding rates for {Symbol}", 
			allFundingRateData.Count, symbolName);
	        
	    return allFundingRateData.OrderBy(f => f.FundingTime);
	}
	
	/// <inheritdoc/>
	protected override async Task<(decimal FundingRate, DateTime FundingTime, int? FundingIntervalHours)?> FetchFundingRateAsync(string symbolName, CancellationToken cancellationToken = default)
	{
		// Get the most recent funding rate for the symbol
		var response = await bybitClient
			.V5Api
			.ExchangeData
			.GetFundingRateHistoryAsync(
				Category.Linear,
				symbolName,
				limit: 1,
				ct: cancellationToken);

		if (response.Data?.List == null || !response.Data.List.Any())
		{
			logger.LogWarning("No funding rate data found for symbol {Symbol}", symbolName);
			return null;
		}

		var data = response.Data.List.First();

		return (data.FundingRate, data.Timestamp, (int?)null);
	}
	
	/// <inheritdoc/>
	protected override (string SymbolName, DateTime? ListingDate) GetExchangeSymbolInfo(object symbol)
	{
		throw new NotImplementedException("Exchange symbol info not used for Bybit");
	}

	/// <inheritdoc/>
	protected override (string SymbolName, int? FundingIntervalHours, DateTime? LaunchTime) GetFundingSymbolInfo(object symbol)
	{
		var bybitSymbol = (BybitLinearInverseSymbol)symbol;
		return (bybitSymbol.Name, bybitSymbol.FundingInterval / 60, bybitSymbol.LaunchTime); // Convert minutes to hours
	}
}