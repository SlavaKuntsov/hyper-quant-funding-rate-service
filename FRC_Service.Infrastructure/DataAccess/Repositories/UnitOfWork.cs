using FRC_Service.Domain.Repositories;

namespace FRC_Service.Infrastructure.DataAccess.Repositories;

/// <inheritdoc/>
public class UnitOfWork(ApplicationDbContext context) : IUnitOfWork
{
    /// <inheritdoc/>
    public async Task SaveAsync(CancellationToken cancellationToken = default)
    {
        await context.SaveChangesAsync(cancellationToken);
    }
}
