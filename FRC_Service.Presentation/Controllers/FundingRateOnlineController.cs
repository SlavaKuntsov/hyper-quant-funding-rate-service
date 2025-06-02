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
[Route("api/v1/funding-online")]
[Produces("application/json")]
[Tags("FundingOnline")]
public class FundingRateOnlineController(
	BinanceFundingRateOnlineService binanceService,
	BybitFundingRateOnlineService bybitService,
	HyperLiquidFundingRateOnlineService hyperLiquidService,
	MexcFundingRateOnlineService mexcService)
	: ControllerBase
{
	/// <summary>
	/// Create a new funding that requires updating for Binance exchange.
	/// </summary>
	/// <param name="exchangeName">The exchange name</param>
	/// <param name="cancellationToken"></param>
	/// <returns>A collection of fundings.</returns>
	/// <response code="200">Returns the list of fundings</response>
	[HttpPost("{exchangeName}")]
	[ProducesResponseType(StatusCodes.Status200OK, Type = typeof(SymbolFundingRatesDto))]
	public async Task<ActionResult> GetBinance(
		[FromRoute] ExchangeCodeType exchangeName = ExchangeCodeType.Binance,
		CancellationToken cancellationToken = default)
	{
		var fundings = exchangeName switch
		{
			ExchangeCodeType.Binance => await binanceService.UpdateOnlineFundingsAsync(cancellationToken),
			ExchangeCodeType.Bybit => await bybitService.UpdateOnlineFundingsAsync(cancellationToken),
			ExchangeCodeType.HyperLiquid => await hyperLiquidService.UpdateOnlineFundingsAsync(cancellationToken),
			ExchangeCodeType.Mexc => await mexcService.UpdateOnlineFundingsAsync(cancellationToken),
			_ => null
		};
		
		return Ok(fundings.Count());
	}

	/// <summary>
	/// Gets funding rates for specific exchange.
	/// </summary>
	/// <param name="exchangeName">The exchange name</param>
	/// <param name="pageNumber">The page number (1-based)</param>
	/// <param name="pageSize">The number of items per page</param>
	/// <param name="cancellationToken"></param>
	/// <returns>A collection of fundings.</returns>
	/// <response code="200">Returns the collection of funding rates</response>
	[HttpGet("{exchangeName}")]
	[ProducesResponseType(StatusCodes.Status200OK)]
	public async Task<ActionResult> GetExchangeFundingRates(
		[FromRoute] ExchangeCodeType exchangeName,
		[FromQuery] int pageNumber = 1,
		[FromQuery] int pageSize = 10,
		CancellationToken cancellationToken = default)
	{
		if (pageSize > 1000) pageSize = 1000;
		if (pageNumber < 1) pageNumber = 1;
		
		var fundings = await binanceService.GetExchangeFundingRatesAsync(
			exchangeName,
			pageNumber,
			pageSize,
			cancellationToken);
        
		return Ok(fundings);
	}
	
	/// <summary>
	/// Gets last fundings for all exchanges.
	/// </summary>
	/// <param name="pageNumber">The page number</param>
	/// <param name="pageSize">The number of items per page</param>
	/// <param name="cancellationToken"></param>
	/// <returns>A collection of fundings.</returns>
	/// <response code="200">Returns the dictionary of funding rates for all exchanges</response>
	[HttpGet]
	[ProducesResponseType(StatusCodes.Status200OK)]
	public async Task<ActionResult> GetLatestFundingRates(
		[FromQuery] int pageNumber = 1,
		[FromQuery] int pageSize = 10,
		CancellationToken cancellationToken = default)
	{
		if (pageSize > 1000) pageSize = 1000;
		if (pageNumber < 1) pageNumber = 1;
		
		var fundings = await binanceService.GetSymbolsFundingRatesAsync(
			pageNumber, 
			pageSize, 
			cancellationToken);

		return Ok(fundings);
	}
}