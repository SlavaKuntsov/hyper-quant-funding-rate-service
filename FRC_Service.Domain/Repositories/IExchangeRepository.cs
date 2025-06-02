using System.Linq.Expressions;
using FRC_Service.Domain.Enums;
using FRC_Service.Domain.Models;

namespace FRC_Service.Domain.Repositories;

/// <summary>
/// Repository to operate with exchanges
/// </summary>
public interface IExchangeRepository
{
    /// <summary>
    /// Gets all exchanges
    /// </summary>
    /// <param name="cancellationToken"></param>
    /// <returns>Collection of all exchanges</returns>
    Task<IEnumerable<Exchange>> GetAllAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets exchanges that match the specified filter
    /// </summary>
    /// <param name="filter">Expression to filter exchanges</param>
    /// <param name="cancellationToken"></param>
    /// <returns>Collection of filtered exchanges</returns>
    Task<IEnumerable<Exchange>> GetByFilterAsync(
        Expression<Func<Exchange, bool>> filter, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets an exchange by name
    /// </summary>
    /// <param name="code">The exchange name</param>
    /// <param name="cancellationToken"></param>
    /// <returns>The exchange if found, otherwise null</returns>
    Task<Exchange?> GetByCodeAsync(ExchangeCodeType code, CancellationToken cancellationToken = default);

    /// <summary>
    /// Adds a new exchange
    /// </summary>
    /// <param name="exchange">The exchange to add</param>
    /// <param name="cancellationToken"></param>
    /// <returns>The added exchange with generated ID</returns>
    Task AddAsync(Exchange exchange, CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates an existing exchange
    /// </summary>
    /// <param name="exchange">The exchange with updated values</param>
    /// <param name="cancellationToken"></param>
    Task UpdateAsync(Exchange exchange, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes an exchange
    /// </summary>
    /// <param name="exchange">The exchange to delete</param>
    /// <param name="cancellationToken"></param>
    Task DeleteAsync(Exchange exchange, CancellationToken cancellationToken = default);
}