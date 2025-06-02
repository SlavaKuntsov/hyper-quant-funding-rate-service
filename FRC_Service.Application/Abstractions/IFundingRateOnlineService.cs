using FRC_Service.Application.Dtos;
using FRC_Service.Domain.Enums;

namespace FRC_Service.Application.Abstractions;

/// <summary>
/// Service for managing fundings.
/// </summary>
public interface IFundingRateOnlineService
{
	/// <summary>
	/// Adds a new fundings.
	/// </summary>
	/// <param name="cancellationToken"></param>
	/// <returns>The collection of all updated funding rates</returns>
	Task<IEnumerable<FundingRateDto>> UpdateOnlineFundingsAsync(CancellationToken cancellationToken = default);
	
	/// <summary>
	/// Gets symbol funding rates across all exchanges with pagination
	/// </summary>
	/// <param name="pageNumber">The page number</param>
	/// <param name="pageSize">The number of items per page</param>
	/// <param name="cancellationToken"></param>
	/// <returns>The pagination response of latest funding rates by symbol dictionary</returns>
	Task<PaginationDto<List<SymbolFundingRatesDto>>> GetSymbolsFundingRatesAsync(
		int pageNumber = 1,
		int pageSize = 10,
		CancellationToken cancellationToken = default);

	/// <summary>
	/// Gets funding rates for a specific exchange with pagination
	/// </summary>
	/// <param name="exchangeCode">The exchange code to get funding rates for</param>
	/// <param name="pageNumber">The page number (1-based)</param>
	/// <param name="pageSize">The number of items per page</param>
	/// <param name="cancellationToken"></param>
	/// <returns>The paginated list of funding rates for the specified exchange</returns>
	Task<PaginationDto<List<FundingRateDto>>> GetExchangeFundingRatesAsync(
		ExchangeCodeType exchangeCode,
		int pageNumber = 1,
		int pageSize = 10,
		CancellationToken cancellationToken = default);
}