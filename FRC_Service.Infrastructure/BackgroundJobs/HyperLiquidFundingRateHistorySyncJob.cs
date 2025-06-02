using FRC_Service.Domain.Enums;
using FRC_Service.Domain.Repositories;
using FRC_Service.Infrastructure.Exceptions;
using Microsoft.Extensions.Logging;
using Quartz;
using UltimaHyperLiquidConnector.Interfaces.Clients;
using UltimaHyperLiquidConnector.Objects.Models;

namespace FRC_Service.Infrastructure.BackgroundJobs;

[DisallowConcurrentExecution]
public class HyperLiquidFundingRateHistorySyncJob(
	ILogger<HyperLiquidFundingRateHistorySyncJob> logger,
	IHyperLiquidRestClient hyperLiquidClient,
	IExchangeRepository exchangeRepository,
	IFundingRateHistoryRepository fundingRateHistoryRepository,
	IUnitOfWork unitOfWork)
	: BaseFundingRateHistorySyncJob<HyperLiquidFundingRateHistorySyncJob>(
		logger,
		exchangeRepository,
		fundingRateHistoryRepository,
		unitOfWork)
{
	private const int FundingInterval = 1;
	
	protected override ExchangeCodeType ExchangeCode => ExchangeCodeType.HyperLiquid;
	protected override int MaxNumbersOfParallelism => 1;
	protected override int BatchSizeForHistory => 30;

	/// <inheritdoc/>
	protected override async Task<IEnumerable<(object? ExchangeSymbol, object? FundingSymbol)>> FetchSymbolsAsync(CancellationToken 
		cancellationToken = default)
	{
		var fundingResponse = await hyperLiquidClient
			.FuturesApi
			.ExchangeData
			.GetExchangeInfoAsync(cancellationToken);

		if (!fundingResponse.Success)
		{
			throw new ExchangeApiException("HyperLiquid", null, "Failed to fetch funding info");
		}

		// Filter for linear perpetual contracts only
		var fundingSymbols = fundingResponse.Data
			.Distinct()
			.Select(f => (ExchangeSymbol: (object?)null, FundingSymbol: (object?)f))
			.ToList();

		logger.LogInformation("Found {Count} linear perpetual symbols with funding rates", fundingSymbols.Count);
    
		return fundingSymbols;
	}
	
	/// <inheritdoc/>
	protected override async Task<IEnumerable<(decimal FundingRate, DateTime FundingTime, int? FundingIntervalHours)>> FetchAllFundingRatesAsync(
	    string symbolName,
	    DateTime? startTime = null,
	    CancellationToken cancellationToken = default)
	{
	    var allFundingRateData = new List<(decimal FundingRate, DateTime FundingTime, int? FundingIntervalHours)>();
	    var hasMoreData = true;
	    
	    // If no start time provided, use a very early date to get all available data
	    var effectiveStartTime = startTime ?? new DateTime(2000, 1, 1, 0, 0, 0, DateTimeKind.Utc);
	    
	    // Paginate through historical data until we reach the present
	    while (hasMoreData && !cancellationToken.IsCancellationRequested)
	    {
	        var response = await hyperLiquidClient
	            .FuturesApi
	            .ExchangeData
	            .GetFundingRateHistoryAsync(
	                symbolName,
	                startTime: effectiveStartTime,
	                ct: cancellationToken);
	        
	        if (!response.Success)
	        {
	            logger.LogWarning("Failed to fetch funding rates for {Symbol} starting from {StartTime}", 
	                symbolName, effectiveStartTime);
	            break;
	        }

	        // Check if we've reached the end of available data
	        if (!response.Data?.Any() == true)
	        {
	            hasMoreData = false;
	            continue;
	        }
	        
	        var batchCount = response.Data.Count();
	        
	        // Convert HyperLiquid response to our internal tuple format
	        allFundingRateData.AddRange(response.Data.Select(f => 
	            (f.FundingRate, f.Timestamp, (int?)null)));
	        
	        // If we received less than the expected limit, we've likely reached the end
	        if (batchCount < Limit)
	        {
	            hasMoreData = false;
	            continue;
	        }
	        
	        // Prepare for next page - start from the next millisecond after the last item
	        var lastItem = response.Data.Last();
	        effectiveStartTime = lastItem.Timestamp.AddMilliseconds(1);
	        
	        // Respect API rate limits
	        await Task.Delay(700, cancellationToken);
	    }
	    
	    logger.LogInformation("Fetched {Count} total historical funding rates for {Symbol}", 
	        allFundingRateData.Count, symbolName);
	    
	    return allFundingRateData;
	}
	
	protected override async Task<(decimal FundingRate, DateTime FundingTime, int? FundingIntervalHours)?> FetchFundingRateAsync(string symbolName, CancellationToken 
		cancellationToken = default)
	{
		var startTime = DateTime.UtcNow.AddHours(-FundingInterval);
		
		// Get the most recent funding rate for the symbol
		var response = await hyperLiquidClient
			.FuturesApi
			.ExchangeData
			.GetFundingRateHistoryAsync(
				symbolName,
				startTime: startTime,
				ct: cancellationToken);

		if (response.Data == null || !response.Data.Any())
		{
			logger.LogWarning("No funding rate data found for symbol {Symbol}", symbolName);
			return null;
		}

		var data = response.Data.Last();

		return (data.FundingRate, data.Timestamp, (int?)null);
	}

	/// <inheritdoc/>
	protected override (string SymbolName, DateTime? ListingDate) GetExchangeSymbolInfo(object symbol)
	{
		throw new NotImplementedException("Exchange symbol info not used for HyperLiquid");
	}

	/// <inheritdoc/>
	protected override (string SymbolName, int? FundingIntervalHours, DateTime? LaunchTime) GetFundingSymbolInfo(object symbol)
	{
		var hyperLiquidSymbol = (HyperLiquidFuturesSymbol)symbol;
		return (hyperLiquidSymbol.Name, FundingInterval, null);
	}
	
	/// <inheritdoc/>
	protected override async Task LaunchDelayForApi(int count, CancellationToken cancellationToken)
	{
		var delay = count / 10;
		await Task.Delay(delay, cancellationToken); 
		logger.LogInformation("Delay between batches: {Delay}ms", delay);
	}
}