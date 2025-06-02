using System.Linq.Expressions;
using FRC_Service.Domain.Models;

namespace FRC_Service.Domain.Repositories;

/// <summary>
/// Repository to operate with FundingRates
/// </summary>
public interface IFundingRateOnlineRepository
{
    /// <summary>
    /// Gets latest funding rates for paginated symbols
    /// </summary>
    /// <param name="pageNumber">Page number</param>
    /// <param name="pageSize">Number of symbols per page</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of latest funding rate entities</returns>
    Task<IEnumerable<FundingRateOnline>> GetLatestSymbolFundingRatesAsync(
        int pageNumber = 1,
        int pageSize = 10,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets total count of unique symbols for pagination
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Count of unique records</returns>
    Task<int> GetUniqueSymbolsCountAsync(CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Gets count of funding rates matching the filter
    /// </summary>
    /// <param name="filter">Filter expression</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Count of records</returns>
    Task<int> GetCountByFilterAsync(
        Expression<Func<FundingRateOnline, bool>> filter, 
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Gets FundingRates that match the specified filter
    /// </summary>
    /// <param name="filter">Expression to filter FundingRates</param>
    /// <param name="pageNumber">The page number</param>
    /// <param name="pageSize">The number of items per page</param>
    /// <param name="cancellationToken"></param>
    /// <returns>Collection of filtered FundingRates</returns>
    Task<IEnumerable<FundingRateOnline>> GetByFilterAsync(
        Expression<Func<FundingRateOnline, bool>> filter, 
        int? pageNumber = null, 
        int? pageSize = null, 
        CancellationToken cancellationToken = default);
        
    /// <summary>
    /// Gets a single FundingRate that matches the specified filter
    /// </summary>
    /// <param name="filter">Expression to filter FundingRates</param>
    /// <param name="cancellationToken"></param>
    /// <returns>The FundingRate if found, otherwise null</returns>
    Task<FundingRateOnline?> GetSingleByFilterAsync(
        Expression<Func<FundingRateOnline, bool>> filter, 
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Adds a new FundingRate
    /// </summary>
    /// <param name="FundingRateOnline">The FundingRate to add</param>
    /// <param name="cancellationToken"></param>
    /// <returns>The added FundingRate with generated ID</returns>
    Task AddAsync(FundingRateOnline FundingRateOnline, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Adds a range of new FundingRates
    /// </summary>
    /// <param name="fundingRates">FundingRates to add</param>
    /// <param name="cancellationToken"></param>
    /// <returns>The added FundingRate with generated ID</returns>
    Task AddRangeAsync(IEnumerable<FundingRateOnline> fundingRates, CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates an existing FundingRate
    /// </summary>
    /// <param name="FundingRateOnline">The FundingRate with updated values</param>
    /// <param name="cancellationToken"></param>
    Task UpdateAsync(FundingRateOnline FundingRateOnline, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Updates an existing FundingRate
    /// </summary>
    /// <param name="fundingRates">FundingRates with updated values</param>
    /// <param name="cancellationToken"></param>
    Task UpdateRangeAsync(IEnumerable<FundingRateOnline> fundingRates, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes a FundingRate
    /// </summary>
    /// <param name="FundingRateOnline"></param>
    /// <param name="cancellationToken"></param>
    Task DeleteAsync(FundingRateOnline FundingRateOnline, CancellationToken cancellationToken = default);
}