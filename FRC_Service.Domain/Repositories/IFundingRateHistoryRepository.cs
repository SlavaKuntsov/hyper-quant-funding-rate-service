using System.Linq.Expressions;
using FRC_Service.Domain.Enums;
using FRC_Service.Domain.Models;

namespace FRC_Service.Domain.Repositories;

/// <summary>
/// Repository to operate with FundingRates
/// </summary>
public interface IFundingRateHistoryRepository
{
    /// <summary>
    /// Gets latest funding rates with flexible grouping and filtering
    /// </summary>
    /// <param name="filter">Expression to filter FundingRates (null for all records)</param>
    /// <param name="groupByExchange">If true, groups by Symbol+ExchangeId; if false, groups only by Symbol</param>
    /// <param name="pageNumber">Page number (1-based)</param>
    /// <param name="pageSize">Number of items per page</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Collection of latest funding rates</returns>
    Task<IEnumerable<FundingRateHistory>> GetLatestSymbolRatesAsync(
        Expression<Func<FundingRateHistory, bool>>? filter = null,
        bool groupByExchange = false,
        int? pageNumber = null,
        int? pageSize = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets total count of unique symbols for pagination
    /// </summary>
    Task<int> GetUniqueSymbolsCountAsync(
        Expression<Func<FundingRateHistory, bool>>? filter = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets count of funding rates matching the filter
    /// </summary>
    /// <param name="filter">Filter expression</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Count of records</returns>
    Task<int> GetCountByFilterAsync(
        Expression<Func<FundingRateHistory, bool>> filter, 
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets funding rates that match the specified filter with pagination
    /// </summary>
    /// <param name="filter">Expression to filter FundingRates</param>
    /// <param name="pageNumber">The page number</param>
    /// <param name="pageSize">The number of items per page</param>
    /// <param name="cancellationToken"></param>
    /// <returns>Collection of filtered FundingRates</returns>
    Task<IEnumerable<FundingRateHistory>> GetByFilterAsync(
        Expression<Func<FundingRateHistory, bool>> filter, 
        int? pageNumber = null, 
        int? pageSize = null, 
        CancellationToken cancellationToken = default);
        
    /// <summary>
    /// Gets a single FundingRate that matches the specified filter
    /// </summary>
    /// <param name="filter">Expression to filter FundingRates</param>
    /// <param name="cancellationToken"></param>
    /// <returns>The FundingRate if found, otherwise null</returns>
    Task<FundingRateHistory?> GetSingleByFilterAsync(
        Expression<Func<FundingRateHistory, bool>> filter, 
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the latest FundingRate that matches the specified filter
    /// </summary>
    /// <param name="filter">Expression to filter FundingRates</param>
    /// <param name="cancellationToken"></param>
    /// <returns>The FundingRate if found, otherwise null</returns>
    Task<FundingRateHistory?> GetLatestByFilterAsync(
        Expression<Func<FundingRateHistory, bool>> filter,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Adds a new FundingRate
    /// </summary>
    /// <param name="fundingRateHistory">The FundingRate to add</param>
    /// <param name="cancellationToken"></param>
    /// <returns>The added FundingRate with generated ID</returns>
    Task AddAsync(FundingRateHistory fundingRateHistory, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Adds a range of new FundingRates
    /// </summary>
    /// <param name="fundingRates">FundingRates to add</param>
    /// <param name="cancellationToken"></param>
    /// <returns>The added FundingRate with generated ID</returns>
    Task AddRangeAsync(IEnumerable<FundingRateHistory> fundingRates, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Adds a range of new FundingRates with BulkInsert
    /// </summary>
    /// <param name="fundingRates">FundingRates to add</param>
    /// <param name="cancellationToken"></param>
    /// <returns>The added FundingRate with generated ID</returns>
    Task BulkInsertAsync(IEnumerable<FundingRateHistory> fundingRates, CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates an existing FundingRate
    /// </summary>
    /// <param name="fundingRateHistory">The FundingRate with updated values</param>
    /// <param name="cancellationToken"></param>
    Task UpdateAsync(FundingRateHistory fundingRateHistory, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Updates an existing FundingRate
    /// </summary>
    /// <param name="fundingRates">FundingRates with updated values</param>
    /// <param name="cancellationToken"></param>
    Task UpdateRangeAsync(IEnumerable<FundingRateHistory> fundingRates, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes a FundingRate
    /// </summary>
    /// <param name="fundingRateHistory"></param>
    /// <param name="cancellationToken"></param>
    Task DeleteAsync(FundingRateHistory fundingRateHistory, CancellationToken cancellationToken = default);
}