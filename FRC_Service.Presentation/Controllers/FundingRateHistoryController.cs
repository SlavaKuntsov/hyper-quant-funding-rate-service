using FRC_Service.Application.Dtos;
using FRC_Service.Application.Services;
using FRC_Service.Domain.Enums;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace FRC_Service.Presentation.Controllers;

/// <summary>
/// API Controller for managing exchange data
/// </summary>
[ApiController]
[Route("api/v1/funding-history")]
[Produces("application/json")]
[Tags("FundingHistory")]
public class FundingRateHistoryController(
	BinanceFundingRateHistoryService binanceService,
	BybitFundingRateHistoryService bybitService,
	HyperLiquidFundingRateHistoryService hyperLiquidService,
	MexcFundingRateHistoryService mexcService) : ControllerBase
{
	/// <summary>
	/// Create a new funding that requires updating for Binance exchange.
	/// </summary>
	/// <param name="exchangeName">The exchange name</param>
	/// <param name="cancellationToken"></param>
	/// <returns>A collection of fundings.</returns>
	/// <response code="200">Returns the count of fetched fundings</response>
	[HttpPost("{exchangeName}")]
	[ProducesResponseType(StatusCodes.Status200OK, Type = typeof(SymbolFundingRatesDto))]
	public async Task<ActionResult> Create(
		[FromRoute] ExchangeCodeType exchangeName, 
		CancellationToken cancellationToken = default)
	{
		var fundings = exchangeName switch
		{
			ExchangeCodeType.Binance => await binanceService.UpdateHistoricalFundingsAsync(cancellationToken),
			ExchangeCodeType.Bybit => await bybitService.UpdateHistoricalFundingsAsync(cancellationToken),
			ExchangeCodeType.HyperLiquid => await hyperLiquidService.UpdateHistoricalFundingsAsync(cancellationToken),
			ExchangeCodeType.Mexc => await mexcService.UpdateHistoricalFundingsAsync(cancellationToken),
			_ => null
		};

		return Ok(fundings.Count());
	}
	
	/// <summary>
	/// Gets latest funding rates for specific exchange from history
	/// </summary>
	/// <param name="exchangeName">The exchange name</param>
	/// <param name="pageNumber">The page number (1-based)</param>
	/// <param name="pageSize">The number of items per page</param>
	/// <param name="cancellationToken"></param>
	/// <returns>Paginated list of latest funding rates for the exchange</returns>
	/// <response code="200">Returns the paginated list of latest funding rates</response>
	/// <response code="404">Exchange not found</response>
	[HttpGet("{exchangeName}/latest")]
	[ProducesResponseType(StatusCodes.Status200OK, Type = typeof(PaginationDto<List<FundingRateDto>>))]
	[ProducesResponseType(StatusCodes.Status404NotFound)]
	public async Task<ActionResult<PaginationDto<List<FundingRateDto>>>> GetExchangeLatestFundingRates(
		[FromRoute] ExchangeCodeType exchangeName,
		[FromQuery] int pageNumber = 1,
		[FromQuery] int pageSize = 10,
		CancellationToken cancellationToken = default)
	{
		if (pageSize > 1000) pageSize = 1000;
		if (pageNumber < 1) pageNumber = 1;

		var fundings = exchangeName switch
		{
			ExchangeCodeType.Binance => await binanceService.GetExchangeLatestFundingRatesAsync(exchangeName, pageNumber, pageSize, cancellationToken),
			ExchangeCodeType.Bybit => await bybitService.GetExchangeLatestFundingRatesAsync(exchangeName, pageNumber, pageSize, cancellationToken),
			ExchangeCodeType.HyperLiquid => await hyperLiquidService.GetExchangeLatestFundingRatesAsync(exchangeName, pageNumber, pageSize, cancellationToken),
			ExchangeCodeType.Mexc => await mexcService.GetExchangeLatestFundingRatesAsync(exchangeName, pageNumber, pageSize, cancellationToken),
			_ => throw new ArgumentException($"Unsupported exchange: {exchangeName}")
		};

		return Ok(fundings);
	}
	
	/// <summary>
    /// Gets historical funding rates for specific symbol on specific exchange
    /// </summary>
    /// <param name="symbol">The trading symbol (e.g., BTCUSDT)</param>
    /// <param name="exchangeName">The exchange name</param>
    /// <param name="pageNumber">The page number (1-based)</param>
    /// <param name="pageSize">The number of items per page</param>
    /// <param name="cancellationToken"></param>
    /// <returns>Paginated list of historical funding rates</returns>
    /// <response code="200">Returns the paginated list of historical funding rates</response>
    /// <response code="404">Symbol or exchange not found</response>
    [HttpGet("{exchangeName}/symbol/{symbol}/history")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(PaginationDto<List<FundingRateDto>>))]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult> GetSymbolExchangeHistory(
        [FromRoute] ExchangeCodeType exchangeName,
        [FromRoute] string symbol,
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 10,
        CancellationToken cancellationToken = default)
    {
		if (pageSize > 1000) pageSize = 1000;
		if (pageNumber < 1) pageNumber = 1;
		
        var history = exchangeName switch
        {
            ExchangeCodeType.Binance => await binanceService.GetSymbolExchangeHistoryAsync(symbol, exchangeName, pageNumber, pageSize, cancellationToken),
            ExchangeCodeType.Bybit => await bybitService.GetSymbolExchangeHistoryAsync(symbol, exchangeName, pageNumber, pageSize, cancellationToken),
            ExchangeCodeType.HyperLiquid => await hyperLiquidService.GetSymbolExchangeHistoryAsync(symbol, exchangeName, pageNumber, pageSize, cancellationToken),
            ExchangeCodeType.Mexc => await mexcService.GetSymbolExchangeHistoryAsync(symbol, exchangeName, pageNumber, pageSize, cancellationToken),
            _ => throw new ArgumentException($"Unsupported exchange: {exchangeName}")
        };

        return Ok(history);
    }
	
	/// <summary>
	/// Gets latest funding rates for all symbols across all exchanges from history
	/// </summary>
	/// <param name="pageNumber">The page number (1-based)</param>
	/// <param name="pageSize">The number of items per page</param>
	/// <param name="cancellationToken"></param>
	/// <returns>Paginated list of latest funding rates grouped by symbols</returns>
	/// <response code="200">Returns the paginated list of latest funding rates</response>
	[HttpGet("latest")]
	[ProducesResponseType(StatusCodes.Status200OK, Type = typeof(PaginationDto<List<SymbolFundingRatesDto>>))]
	public async Task<ActionResult<PaginationDto<List<SymbolFundingRatesDto>>>> GetLatestFundingRates(
		[FromQuery] int pageNumber = 1,
		[FromQuery] int pageSize = 10,
		CancellationToken cancellationToken = default)
	{
		if (pageSize > 1000) pageSize = 1000;
		if (pageNumber < 1) pageNumber = 1;

		// Use any service since they all work with the same repository
		var fundings = await binanceService.GetLatestSymbolFundingRatesAsync(
			pageNumber, 
			pageSize, 
			cancellationToken);

		return Ok(fundings);
	}
}