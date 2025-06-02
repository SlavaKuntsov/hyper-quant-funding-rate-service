using System.Linq.Expressions;
using FRC_Service.Domain.Models;
using FRC_Service.Domain.Repositories;
using Microsoft.EntityFrameworkCore;

namespace FRC_Service.Infrastructure.DataAccess.Repositories;

/// <inheritdoc/>
public class FundingRateOnlineRepository(ApplicationDbContext context) : IFundingRateOnlineRepository
{
    /// <inheritdoc/>
    public async Task<IEnumerable<FundingRateOnline>> GetLatestSymbolFundingRatesAsync(
        int pageNumber = 1,
        int pageSize = 10,
        CancellationToken cancellationToken = default)
    {
        // Get paginated list of unique symbols
        var symbols = await context.FundingRatesOnline
            .AsNoTracking()
            .Select(f => f.Symbol)
            .Distinct()
            .OrderBy(s => s)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        // Get latest rates for these symbols
        return await context.FundingRatesOnline
            .AsNoTracking()
            .Include(f => f.Exchange)
            .Where(f => symbols.Contains(f.Symbol))
            .GroupBy(f => new { f.Symbol, f.ExchangeId })
            .Select(g => g.OrderByDescending(f => f.TsRate).First())
            .ToListAsync(cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<int> GetUniqueSymbolsCountAsync(CancellationToken cancellationToken = default)
    {
        return await context.FundingRatesOnline
            .AsNoTracking()
            .Select(f => f.Symbol)
            .Distinct()
            .CountAsync(cancellationToken);
    }
    
    /// <inheritdoc/>
    public async Task<int> GetCountByFilterAsync(
        Expression<Func<FundingRateOnline, bool>> filter, 
        CancellationToken cancellationToken = default)
    {
        return await context.FundingRatesOnline
            .AsNoTracking()
            .Where(filter)
            .CountAsync(cancellationToken);
    }
    
    /// <inheritdoc/>
    public async Task<IEnumerable<FundingRateOnline>> GetByFilterAsync(
        Expression<Func<FundingRateOnline, bool>> filter, 
        int? pageNumber = null, 
        int? pageSize = null, 
        CancellationToken cancellationToken = default)
    {
        IQueryable<FundingRateOnline> query = context.FundingRatesOnline
            .AsNoTracking()
            .Where(filter)
            .OrderBy(s => s.Symbol);
            
        if (pageNumber.HasValue && pageSize.HasValue)
        {
            query = query
                .Skip((pageNumber.Value - 1) * pageSize.Value)
                .Take(pageSize.Value);
        }
        
        return await query.ToListAsync(cancellationToken);
    }
    
    /// <inheritdoc/>
    public async Task<FundingRateOnline?> GetSingleByFilterAsync(
        Expression<Func<FundingRateOnline, bool>> filter, 
        CancellationToken cancellationToken = default)
    {
        return await context.FundingRatesOnline
            .SingleOrDefaultAsync(filter, cancellationToken);
    }

    /// <inheritdoc/>
    public Task AddAsync(FundingRateOnline fundingRateOnline, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(fundingRateOnline);

        context.FundingRatesOnline.Add(fundingRateOnline);
        
        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public async Task AddRangeAsync(IEnumerable<FundingRateOnline> fundingRatesOnline, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(fundingRatesOnline);
        
        await context.FundingRatesOnline.AddRangeAsync(fundingRatesOnline, cancellationToken);
    }

    /// <inheritdoc/>
    public Task UpdateAsync(FundingRateOnline fundingRateOnline, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(fundingRateOnline);
        
        context.FundingRatesOnline.Update(fundingRateOnline);
        
        return Task.CompletedTask;
    }

    public Task UpdateRangeAsync(IEnumerable<FundingRateOnline> fundingRatesOnline, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(fundingRatesOnline);
        
        context.FundingRatesOnline.UpdateRange(fundingRatesOnline);
        
        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public Task DeleteAsync(FundingRateOnline fundingRateOnline, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(fundingRateOnline);

        context.FundingRatesOnline.Remove(fundingRateOnline);

        return Task.CompletedTask;
    }
}
