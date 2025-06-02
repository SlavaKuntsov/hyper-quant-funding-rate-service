using FRC_Service.Application.Dtos;
using FRC_Service.Domain.Enums;
using FRC_Service.Domain.Repositories;
using FRC_Service.Infrastructure.Exceptions;
using Microsoft.Extensions.Logging;
using UltimaBinanceConnector.Enums;
using UltimaBinanceConnector.Interfaces.Clients;
using UltimaBinanceConnector.Objects.Models.Futures;

namespace FRC_Service.Application.Services;

/// <inheritdoc />
public class BinanceFundingRateHistoryService(
	ILogger<BinanceFundingRateHistoryService> logger,
	IBinanceRestClient binanceClient,
	IExchangeRepository exchangeRepository,
	IFundingRateHistoryRepository fundingRateHistoryRepository,
	IUnitOfWork unitOfWork)
	: BaseFundingRateHistoryService<BinanceFundingRateHistoryService>(
		logger,
		exchangeRepository,
		fundingRateHistoryRepository,
		unitOfWork)
{
	protected override ExchangeCodeType ExchangeCode => ExchangeCodeType.Binance;
	protected override int MaxNumbersOfParallelism => 1;
	protected override int BatchSizeForHistory => 10;

	/// <inheritdoc/>
	protected override async Task<IEnumerable<SymbolPairDto>> FetchSymbolsAsync(CancellationToken cancellationToken = default)
	{
		// First, get funding info for symbols that have it
		var fundingResponse = await binanceClient
			.UsdFuturesApi
			.ExchangeData
			.GetFundingInfoAsync(cancellationToken);

		if (!fundingResponse.Success || fundingResponse.Data == null)
		{
			logger.LogError(
				"Failed to fetch funding info from Binance: {Error}",
				fundingResponse.Error?.Message ?? "Unknown error");

			throw new ExchangeApiException("Binance", null, "Failed to fetch funding info");
		}

		var fundingSymbols = fundingResponse.Data.Distinct().ToList();

		// Then, get exchange info to filter only active perpetual contracts
		var exchangeResponse = await binanceClient
			.UsdFuturesApi
			.ExchangeData
			.GetExchangeInfoAsync(cancellationToken);

		if (!exchangeResponse.Success || exchangeResponse.Data == null)
		{
			logger.LogError(
				"Failed to fetch exchange info from Binance: {Error}",
				exchangeResponse.Error?.Message ?? "Unknown error");

			throw new ExchangeApiException("Binance", null, "Failed to fetch exchange info");
		}

		// Filter for active perpetual contracts only
		var exchangeSymbols = exchangeResponse.Data.Symbols
			.Distinct()
			.Where(s => s.Status == SymbolStatus.Trading && s.ContractType == ContractType.Perpetual)
			.ToList();

		// Create a set of symbols that already have funding info
		var existingFundingSymbols = new HashSet<string>(
			fundingSymbols.Select(f => f.Symbol),
			StringComparer.OrdinalIgnoreCase);

		// Find symbols from exchange that are missing in funding info
		var missingFundingSymbols = exchangeSymbols
			.Where(e => !existingFundingSymbols.Contains(e.Name))
			.ToList();

		logger.LogInformation(
			"Found {ExchangeCount} active perpetual symbols, {FundingCount} with funding info, {MissingCount} missing funding info",
			exchangeSymbols.Count,
			fundingSymbols.Count,
			missingFundingSymbols.Count);

		// For missing symbols, determine funding interval from history
		var additionalFundingSymbols = new List<BinanceFuturesFundingInfo>();

		foreach (var missingSymbol in missingFundingSymbols)
		{
			try
			{
				var fundingInterval = await DetermineFundingIntervalAsync(
					missingSymbol.Name,
					cancellationToken);

				if (fundingInterval.HasValue)
				{
					var additionalFundingInfo = new BinanceFuturesFundingInfo
					{
						Symbol = missingSymbol.Name,
						FundingIntervalHours = fundingInterval.Value
					};

					additionalFundingSymbols.Add(additionalFundingInfo);
				}
				else
				{
					logger.LogWarning(
						"Could not determine funding interval for {Symbol}, skipping",
						missingSymbol.Name);
				}

				// Small delay to respect rate limits
				await Task.Delay(100, cancellationToken);
			}
			catch (Exception ex)
			{
				logger.LogWarning(
					ex,
					"Error determining funding interval for {Symbol}, skipping",
					missingSymbol.Name);
			}
		}

		fundingSymbols.AddRange(additionalFundingSymbols);
		
		// Join funding and exchange data to get complete symbol information
		var intersectSymbols = exchangeSymbols
			.Join(
				fundingSymbols,
				exchange => exchange.Name.ToUpper(),
				funding => funding.Symbol.ToUpper(),
				(exchange, funding) => new SymbolPairDto(exchange, funding) 
			).ToList();

		logger.LogInformation(
			"Final result: {Count} active perpetual symbols with funding rates",
			intersectSymbols.Count);

		return intersectSymbols;
	}

	/// <summary>
	/// Determines funding interval by analyzing the last 2 funding rates
	/// </summary>
	private async Task<int?> DetermineFundingIntervalAsync(string symbolName, CancellationToken cancellationToken)
	{
		try
		{
			var response = await binanceClient
				.UsdFuturesApi
				.ExchangeData
				.GetFundingRatesAsync(
					symbol: symbolName,
					limit: 2,
					ct: cancellationToken);

			if (!response.Success || response.Data == null || response.Data.Count() < 2)
			{
				return null;
			}

			var rates = response.Data.OrderByDescending(x => x.FundingTime).ToList();
          
			// Calculate time difference between the two most recent funding times
			var timeDiff = rates[0].FundingTime - rates[1].FundingTime;
			var hoursInterval = (int)Math.Round(timeDiff.TotalHours);
          
			// Validate that the interval is reasonable (1-24 hours)
			if (hoursInterval > 0 && hoursInterval <= 24)
			{
				return hoursInterval;
			}

			logger.LogWarning("Invalid funding interval calculated for {Symbol}: {Hours}h", symbolName, hoursInterval);
			return null;
		}
		catch (Exception ex)
		{
			logger.LogWarning(ex, "Error fetching funding rates for interval calculation for {Symbol}", symbolName);
			return null;
		}
	}
	
	/// <inheritdoc />
	protected override async Task<IEnumerable<FuturesDataDto>> FetchAllFundingRatesAsync(
		string symbolName,
		DateTime? startTime,
		CancellationToken cancellationToken = default)
	{
		var allFundingRateData = new List<FuturesDataDto>();
		var hasMoreData = true;

		// Paginate through historical data until we reach the present
		while (hasMoreData && !cancellationToken.IsCancellationRequested)
		{
			var response = await binanceClient
				.UsdFuturesApi
				.ExchangeData
				.GetFundingRatesAsync(
					symbolName,
					startTime,
					limit: Limit,
					ct: cancellationToken);

			if (!response.Success)
			{
				logger.LogWarning(
					"Failed to fetch funding rates for {Symbol} starting from {StartTime}",
					symbolName,
					startTime);

				break;
			}

			// Check if we've reached the end of available data
			if (!response.Data.Any())
			{
				hasMoreData = false;

				continue;
			}

			// Convert Binance response to our internal DTO format
			allFundingRateData.AddRange(
				response.Data.Select(
					f =>
						new FuturesDataDto(f.FundingRate, f.FundingTime)));

			// If we received less than the limit, we've reached the end
			if (response.Data.Count() < Limit)
			{
				hasMoreData = false;

				continue;
			}

			// Prepare for next page - start from the next millisecond after the last item
			var lastItem = response.Data.Last();
			startTime = lastItem.FundingTime.AddMilliseconds(1);

			// Respect API rate limits
			await Task.Delay(400, cancellationToken);
		}

		logger.LogDebug(
			"Fetched {Count} historical funding rates for {Symbol}",
			allFundingRateData.Count,
			symbolName);

		return allFundingRateData;
	}
	/// <inheritdoc />
	protected override async Task<FuturesDataDto?> FetchFundingRateAsync(
		string symbolName,
		CancellationToken cancellationToken = default)
	{
		// Get the most recent funding rate for the symbol
		var response = await binanceClient
			.UsdFuturesApi
			.ExchangeData
			.GetFundingRatesAsync(symbolName, limit: 1, ct: cancellationToken);

		if (response.Data == null || !response.Data.Any())
		{
			logger.LogWarning("No funding rate data found for symbol {Symbol}", symbolName);

			return null;
		}

		var data = response.Data.First();

		return new FuturesDataDto(
			data.FundingRate,
			data.FundingTime);
	}
	/// <inheritdoc />
	protected override ExchangeInfoSymbol GetExchangeInfoSymbol(object symbol)
	{
		return new ExchangeInfoSymbol(
			((BinanceFuturesUsdtSymbol)symbol).Name,
			((BinanceFuturesUsdtSymbol)symbol).ListingDate);
	}
	/// <inheritdoc />
	protected override FundingInfoSymbol GetFundingInfoSymbol(object symbol)
	{
		return new FundingInfoSymbol(
			((BinanceFuturesFundingInfo)symbol).Symbol,
			((BinanceFuturesFundingInfo)symbol).FundingIntervalHours,
			null);
	}
	/// <inheritdoc />
	protected override async Task LaunchDelayForApi(int count, CancellationToken cancellationToken)
	{
		var delay = count / 10;
		await Task.Delay(delay, cancellationToken);
		logger.LogInformation("Delay between batches: {Delay}ms", delay);
	}
}