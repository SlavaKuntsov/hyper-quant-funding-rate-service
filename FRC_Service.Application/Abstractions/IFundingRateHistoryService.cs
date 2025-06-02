using FRC_Service.Application.Dtos;
using FRC_Service.Domain.Enums;
using FRC_Service.Domain.Models;

namespace FRC_Service.Application.Abstractions;

/// <summary>
/// Service for managing fundings.
/// </summary>
public interface IFundingRateHistoryService
{
	/// <summary>
	/// Adds a new fundings.
	/// </summary>
	/// <param name="cancellationToken"></param>
	/// <returns>The added exchange with generated ID</returns>
    Task<IEnumerable<FundingRateDto>> UpdateHistoricalFundingsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets latest funding rates for specific exchange with pagination
    /// </summary>
    /// <param name="exchangeCode">The exchange code</param>
    /// <param name="pageNumber">The page number (1-based)</param>
    /// <param name="pageSize">The number of items per page</param>
    /// <param name="cancellationToken"></param>
    /// <returns>Paginated list of latest funding rates for the exchange</returns>
    Task<PaginationDto<List<FundingRateDto>>> GetExchangeLatestFundingRatesAsync(
        ExchangeCodeType exchangeCode,
        int pageNumber = 1,
        int pageSize = 10,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets historical funding rates for specific symbol on specific exchange
    /// </summary>
    /// <param name="symbol">The trading symbol</param>
    /// <param name="exchangeCode">The exchange code</param>
    /// <param name="pageNumber">The page number (1-based)</param>
    /// <param name="pageSize">The number of items per page</param>
    /// <param name="cancellationToken"></param>
    /// <returns>Paginated list of historical funding rates</returns>
    Task<PaginationDto<List<FundingRateDto>>> GetSymbolExchangeHistoryAsync(
        string symbol,
        ExchangeCodeType exchangeCode,
        int pageNumber = 1,
        int pageSize = 10,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets historical funding rates for specific symbol across all exchanges
    /// </summary>
    /// <param name="pageNumber">The page number (1-based)</param>
    /// <param name="pageSize">The number of items per page</param>
    /// <param name="cancellationToken"></param>
    /// <returns>Paginated list of historical funding rates grouped by exchanges</returns>
    Task<PaginationDto<List<SymbolFundingRatesDto>>> GetLatestSymbolFundingRatesAsync(
        int pageNumber = 1,
        int pageSize = 10,
        CancellationToken cancellationToken = default);
}