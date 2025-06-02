using FRC_Service.Domain.Enums;
using FRC_Service.Domain.Repositories;
using FRC_Service.Infrastructure.Exceptions;
using Microsoft.Extensions.Logging;
using Quartz;
using UltimaMexcConnector.Interfaces.Clients;
using UltimaMexcConnector.Objects.Models.Futures;

namespace FRC_Service.Infrastructure.BackgroundJobs;

[DisallowConcurrentExecution]
public class MexcFundingRateHistorySyncJob(
	ILogger<MexcFundingRateHistorySyncJob> logger,
	IMexcRestClient mexcClient,
	IExchangeRepository exchangeRepository,
	IFundingRateHistoryRepository fundingRateHistoryRepository,
	IUnitOfWork unitOfWork)
	: BaseFundingRateHistorySyncJob<MexcFundingRateHistorySyncJob>(
		logger,
		exchangeRepository,
		fundingRateHistoryRepository,
		unitOfWork)
{
	protected override ExchangeCodeType ExchangeCode => ExchangeCodeType.Mexc;
	protected override int MaxNumbersOfParallelism => 3;
	protected override int BatchSizeForHistory => 30;

	/// <inheritdoc/>
	protected override async Task<IEnumerable<(object? ExchangeSymbol, object? FundingSymbol)>> FetchSymbolsAsync(CancellationToken 
		cancellationToken = default)
	{
		var fundingResponse = await mexcClient
			.FuturesApi
			.ExchangeData
			.GetContractDetailsAsync(ct: cancellationToken);

		if (!fundingResponse.Success)
		{
			throw new ExchangeApiException("Mexc", null, "Failed to fetch funding info");
		}
		
		// Filter for linear perpetual contracts only
		var fundingSymbols = fundingResponse.Data.Symbols
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
	    var currentPage = 1;
	    int? totalPages = null;
	    
	    // Paginate through all pages until we reach the end
	    while (!cancellationToken.IsCancellationRequested)
	    {
	        var response = await mexcClient
	            .FuturesApi
	            .ExchangeData
	            .GetFundingRateHistoryAsync(
	                symbolName,
	                pageNum: currentPage,
	                pageSize: Limit,
	                ct: cancellationToken);
	        
	        if (!response.Success || !response.Data.Success)
	        {
	            logger.LogWarning("Failed to fetch funding rates for {Symbol} on page {Page}", 
	                symbolName, currentPage);
	            break;
	        }

			if (response.Data.Data?.ResultList == null)
			{
				logger.LogWarning("No data received for {Symbol} on page {Page}", 
					symbolName, currentPage);
				break;
			}
	        
	        // Get pagination info from first response
	        if (totalPages == null)
	        {
	            totalPages = response.Data.Data.TotalPage;
	        }

			if (response.Data.Data == null || response.Data.Data.ResultList == null)
			{
				logger.LogWarning("Received null data for {Symbol} on page {Page}", 
					symbolName, currentPage);
				break;
			}
	        
	        var records = response.Data.Data.ResultList;
	        
	        // Convert MEXC response to our internal tuple format
	        var convertedRecords = records.Select(f => 
	            (f.FundingRate, f.Timestamp, (int?)f.FundingInterval));
	        
	        // If startTime filter is specified, filter records
	        if (startTime.HasValue)
	        {
	            convertedRecords = convertedRecords.Where(f => f.Timestamp >= startTime.Value);
	        }
	        
	        allFundingRateData.AddRange(convertedRecords);
	        
	        // Check if we've reached the last page
	        if (currentPage >= totalPages)
	        {
	            break;
	        }
	        
	        // Move to next page
	        currentPage++;
	        
	        // Respect API rate limits (20 requests per 2 seconds = 0.1s delay)
	        await Task.Delay(500, cancellationToken);
	    }
	    
	    // Sort data chronologically (oldest first) since MEXC returns newest first
	    var sortedData = allFundingRateData.OrderBy(f => f.FundingTime).ToList();
	    
	    logger.LogInformation("Fetched {Count} historical funding rates for {Symbol} from {Pages} pages", 
	        sortedData.Count, symbolName, currentPage);
	    
	    return sortedData;
	}
	
	/// <inheritdoc/>
	protected override async Task<(decimal FundingRate, DateTime FundingTime, int? FundingIntervalHours)?> FetchFundingRateAsync(string symbolName, CancellationToken cancellationToken = default)
	{
		// Get the most recent funding rate for the symbol
		var response = await mexcClient
			.FuturesApi
			.ExchangeData
			.GetFundingRateAsync(symbolName, ct: cancellationToken);

		if (response.Data == null)
		{
			logger.LogWarning("No funding rate data found for symbol {Symbol}", symbolName);
			return null;
		}

		return (response.Data.FundingRate, response.Data.Timestamp, response.Data.CollectCycle);
	}
	
	/// <inheritdoc/>
	protected override (string SymbolName, DateTime? ListingDate) GetExchangeSymbolInfo(object symbol)
	{
		throw new NotImplementedException("Exchange symbol info not used for MEXC");
	}

	/// <inheritdoc/>
	protected override (string SymbolName, int? FundingIntervalHours, DateTime? LaunchTime) GetFundingSymbolInfo(object symbol)
	{
		var mexcSymbol = (MexcFuturesSymbol)symbol;
		
		// Converting unix time in milliseconds to DateTime
		var launchTime = DateTimeOffset.FromUnixTimeMilliseconds(mexcSymbol.CreateTime).DateTime;
		
		// There is no symbol FundingInterval in Mexc exchange
		return (mexcSymbol.Name, null, launchTime);
	}
}