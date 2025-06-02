using FRC_Service.Application.Dtos;
using FRC_Service.Domain.Enums;
using FRC_Service.Domain.Repositories;
using FRC_Service.Infrastructure.Exceptions;
using Microsoft.Extensions.Logging;
using UltimaMexcConnector.Interfaces.Clients;
using UltimaMexcConnector.Objects.Models.Futures;

namespace FRC_Service.Application.Services;

/// <inheritdoc/>
public class MexcFundingRateHistoryService(
	ILogger<MexcFundingRateHistoryService> logger,
	IMexcRestClient mexcClient,
	IExchangeRepository exchangeRepository,
	IFundingRateHistoryRepository fundingRateHistoryRepository,
	IUnitOfWork unitOfWork)
	: BaseFundingRateHistoryService<MexcFundingRateHistoryService>(
		logger,
		exchangeRepository,
		fundingRateHistoryRepository,
		unitOfWork)
{
	protected override ExchangeCodeType ExchangeCode => ExchangeCodeType.Mexc;
	protected override int MaxNumbersOfParallelism => 3;
	protected override int BatchSizeForHistory => 30;

	/// <inheritdoc/>
	protected override async Task<IEnumerable<SymbolPairDto>> FetchSymbolsAsync(CancellationToken 
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
			.Select(f => new SymbolPairDto(null, f))
			.ToList();

		logger.LogInformation("Found {Count} linear perpetual symbols with funding rates", fundingSymbols.Count);
    
		return fundingSymbols;
	}
	
	/// <inheritdoc/>
	protected override async Task<IEnumerable<FuturesDataDto>> FetchAllFundingRatesAsync(
	    string symbolName,
	    DateTime? startTime,
	    CancellationToken cancellationToken = default)
	{
	    var allFundingRateData = new List<FuturesDataDto>();
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
				Console.WriteLine("qe");
			}
	        
	        var records = response.Data.Data.ResultList;
	        
	        // Convert MEXC response to our internal DTO format
	        var convertedRecords = records.Select(f => 
	            new FuturesDataDto(f.FundingRate, f.Timestamp, f.FundingInterval));
	        
	        // If startTime filter is specified, filter records
	        if (startTime.HasValue)
	        {
	            convertedRecords = convertedRecords.Where(f => f.FundingTime >= startTime.Value);
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
	protected override async Task<FuturesDataDto?> FetchFundingRateAsync(string symbolName, CancellationToken cancellationToken = default)
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

		return new FuturesDataDto(
			response.Data.FundingRate,
			response.Data.Timestamp,
			response.Data.CollectCycle);
	}
	
	/// <inheritdoc/>
	protected override ExchangeInfoSymbol GetExchangeInfoSymbol(object symbol)
	{
		throw new NotImplementedException();
	}

	/// <inheritdoc/>
	protected override FundingInfoSymbol GetFundingInfoSymbol(object symbol)
	{
		// Converting unix time in milliseconds to DateTime
		var launchTime = ((MexcFuturesSymbol)symbol).CreateTime;
		
		// There is no symbol FundingInterval in Mexc exchange
		return new FundingInfoSymbol(
			((MexcFuturesSymbol)symbol).Name,
			null,
			DateTimeOffset.FromUnixTimeMilliseconds(launchTime).DateTime);
	}
}