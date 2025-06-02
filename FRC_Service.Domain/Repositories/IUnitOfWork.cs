namespace FRC_Service.Domain.Repositories;

/// <summary>
/// Unit of work
/// </summary>
public interface IUnitOfWork
{
    /// <summary>
    /// Saves all collected changes to database in a single transaction
    /// </summary>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    Task SaveAsync(CancellationToken cancellationToken = default);
}