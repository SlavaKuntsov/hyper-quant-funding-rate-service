using FRC_Service.Application.Dtos;
using FRC_Service.Domain.Enums;
using FRC_Service.Domain.Repositories;
using FRC_Service.Infrastructure.Exceptions;
using Microsoft.Extensions.Logging;
using UltimaBybitConnector.Enums;
using UltimaBybitConnector.Interfaces.Clients;
using UltimaBybitConnector.Objects.Models.V5;

namespace FRC_Service.Application.Services;

/// <inheritdoc/>
public class BybitFundingRateHistoryService(
	ILogger<BybitFundingRateHistoryService> logger,
	IBybitRestClient bybitClient,
	IExchangeRepository exchangeRepository,
	IFundingRateHistoryRepository fundingRateHistoryRepository,
	IUnitOfWork unitOfWork)
	: BaseFundingRateHistoryService<BybitFundingRateHistoryService>(
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
	protected override async Task<IEnumerable<SymbolPairDto>> FetchSymbolsAsync(CancellationToken cancellationToken = default)
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
			throw new ExchangeApiException("Binance", null, "Failed to fetch funding info");
		}

		// Filter for linear perpetual contracts only
		var fundingSymbols = fundingResponse.Data.List
			.Where(s => s.ContractType == ContractTypeV5.LinearPerpetual)
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
	            
			// Convert Bybit response to our internal DTO format
	        allFundingRateData.AddRange(validItems.Select(f => 
	            new FuturesDataDto(f.FundingRate, f.Timestamp)));
	            
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
	protected override async Task<FuturesDataDto?> FetchFundingRateAsync(string symbolName, CancellationToken cancellationToken = default)
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

		return new FuturesDataDto(
			data.FundingRate,
			data.Timestamp);
	}
	
	/// <inheritdoc/>
	protected override ExchangeInfoSymbol GetExchangeInfoSymbol(object symbol)
	{
		throw new NotImplementedException();
	}

	/// <inheritdoc/>
	protected override FundingInfoSymbol GetFundingInfoSymbol(object symbol)
	{
		return new FundingInfoSymbol(
			((BybitLinearInverseSymbol)symbol).Name,
			((BybitLinearInverseSymbol)symbol).FundingInterval / 60, // Convert minutes to hours
			((BybitLinearInverseSymbol)symbol).LaunchTime);
	}
}