using FRC_Service.Application.Dtos;
using FRC_Service.Domain.Enums;
using FRC_Service.Domain.Repositories;
using FRC_Service.Infrastructure.Exceptions;
using Microsoft.Extensions.Logging;
using UltimaHyperLiquidConnector.Interfaces.Clients;
using UltimaHyperLiquidConnector.Objects.Models;

namespace FRC_Service.Application.Services;

/// <inheritdoc/>
public class HyperLiquidFundingRateOnlineService(
	ILogger<HyperLiquidFundingRateOnlineService> logger,
	IHyperLiquidRestClient hyperLiquidClient,
	IExchangeRepository exchangeRepository,
	IFundingRateOnlineRepository fundingRateOnlineRepository,
	IUnitOfWork unitOfWork)
	: BaseFundingRateOnlineService<HyperLiquidFundingRateOnlineService>(
		logger, exchangeRepository, fundingRateOnlineRepository, unitOfWork)
{
	/// <summary>
	/// HyperLiquid funding interval in hours
	/// </summary>
	private const int FundingInterval = 1;
	
	protected override ExchangeCodeType ExchangeCode => ExchangeCodeType.HyperLiquid;
	protected override int MaxNumbersOfParallelism => 5;
	
	/// <inheritdoc/>
	protected override async Task<IEnumerable<object>> FetchSymbolsAsync(CancellationToken cancellationToken = default)
	{
		// Get exchange info to retrieve all available futures symbols
		var response = await hyperLiquidClient
			.FuturesApi
			.ExchangeData
			.GetExchangeInfoAsync(cancellationToken);

		if (!response.Success || response.Data == null)
		{
			logger.LogError("Failed to fetch exchange info from HyperLiquid: {Error}", 
				response.Error?.Message ?? "Unknown error");
			throw new ExchangeApiException("HyperLiquid", null, "Failed to fetch exchange info");
		}

		// All symbols from HyperLiquid are perpetual futures by default
		var symbols = response.Data.Distinct().ToList();

		logger.LogInformation("Found {Count} perpetual futures symbols", symbols.Count);

		return symbols;
	}

	/// <inheritdoc/>
	protected override async Task<FuturesDataDto?> FetchFuturesAsync(string symbolName, CancellationToken cancellationToken = default)
	{
		// Calculate start time for funding rate lookup (last funding interval)
		var startTime = DateTime.UtcNow.AddHours(-FundingInterval);
		
		var response = await hyperLiquidClient
			.FuturesApi
			.ExchangeData
			.GetFundingRateHistoryAsync(
				symbolName,
				startTime: startTime,
				ct: cancellationToken);

		if (!response.Success)
		{
			logger.LogError("Failed to fetch funding rate for {Symbol}: {Error}", 
				symbolName, response.Error?.Message ?? "Unknown error");
			throw new ExchangeApiException("HyperLiquid", null, $"Failed to fetch funding rate for {symbolName}");
		}

		// Check if data is available
		if (response.Data == null || !response.Data.Any())
		{
			logger.LogWarning("No funding rate data available for {Symbol}", symbolName);
			return null;
		}

		// Get the most recent funding rate entry
		var data = response.Data.First();

		return new FuturesDataDto(
			data.FundingRate,
			data.Timestamp);
	}
    
	/// <inheritdoc/>
	protected override string GetSymbolName(object symbol)
	{
		return ((HyperLiquidFuturesSymbol)symbol).Name;
	}
}