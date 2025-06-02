using System.Linq.Expressions;
using EFCore.BulkExtensions;
using FRC_Service.Domain.Enums;
using FRC_Service.Domain.Models;
using FRC_Service.Domain.Repositories;
using Microsoft.EntityFrameworkCore;

namespace FRC_Service.Infrastructure.DataAccess.Repositories;

/// <inheritdoc/>
public class FundingRateHistoryRepository(ApplicationDbContext context) : IFundingRateHistoryRepository
{
    /// <inheritdoc/>
    public async Task<IEnumerable<FundingRateHistory>> GetLatestSymbolRatesAsync(
        Expression<Func<FundingRateHistory, bool>>? filter = null,
        bool groupByExchange = false,
        int? pageNumber = null,
        int? pageSize = null,
        CancellationToken cancellationToken = default)
    {
        var baseQuery = context.FundingRatesHistory.AsNoTracking();
        
        // Apply filter if provided
        if (filter != null)
        {
            baseQuery = baseQuery.Where(filter);
        }

        IQueryable<FundingRateHistory> query;

        if (groupByExchange)
        {
            // Group by Symbol + ExchangeId (latest per symbol per exchange)
            if (pageNumber.HasValue && pageSize.HasValue)
            {
                // Get paginated symbols first
                var symbols = await baseQuery
                    .Select(f => f.Symbol)
                    .Distinct()
                    .OrderBy(s => s)
                    .Skip((pageNumber.Value - 1) * pageSize.Value)
                    .Take(pageSize.Value)
                    .ToListAsync(cancellationToken);

                query = context.FundingRatesHistory
                    .AsNoTracking()
                    .Include(f => f.Exchange)
                    .Where(f => symbols.Contains(f.Symbol))
                    .GroupBy(f => new { f.Symbol, f.ExchangeId })
                    .Select(g => g.OrderByDescending(f => f.TsRate).First());
            }
            else
            {
                query = baseQuery
                    .Include(f => f.Exchange)
                    .GroupBy(f => new { f.Symbol, f.ExchangeId })
                    .Select(g => g.OrderByDescending(f => f.TsRate).First());
            }
        }
        else
        {
            // Group only by Symbol (latest per symbol across all exchanges)
            query = baseQuery
                .Include(f => f.Exchange)
                .GroupBy(fr => fr.Symbol)
                .Select(g => g.OrderByDescending(fr => fr.TsRate).First());

            if (pageNumber.HasValue && pageSize.HasValue)
            {
                query = query
                    .Skip((pageNumber.Value - 1) * pageSize.Value)
                    .Take(pageSize.Value);
            }
        }

        return await query.ToListAsync(cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<int> GetUniqueSymbolsCountAsync(
        Expression<Func<FundingRateHistory, bool>> filter = null, 
        CancellationToken cancellationToken = default)
    {
        return await context.FundingRatesHistory
            .AsNoTracking()
            .Select(f => f.Symbol)
            .Distinct()
            .CountAsync(cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<int> GetCountByFilterAsync(
        Expression<Func<FundingRateHistory, bool>> filter, 
        CancellationToken cancellationToken = default)
    {
        return await context.FundingRatesHistory
            .AsNoTracking()
            .Where(filter)
            .CountAsync(cancellationToken);
    }
    
    /// <inheritdoc/>
    public async Task<IEnumerable<FundingRateHistory>> GetByFilterAsync(
        Expression<Func<FundingRateHistory, bool>> filter, 
        int? pageNumber = null, 
        int? pageSize = null, 
        CancellationToken cancellationToken = default)
    {
        IQueryable<FundingRateHistory> query = context.FundingRatesHistory
            .AsNoTracking()
            .Include(f => f.Exchange)
            .Where(filter)
            .OrderByDescending(f => f.TsRate); // Order by timestamp desc for history

        if (pageNumber.HasValue && pageSize.HasValue && pageNumber > 0 && pageSize > 0)
        {
            query = query
                .Skip((pageNumber.Value - 1) * pageSize.Value)
                .Take(pageSize.Value);
        }

        return await query.ToListAsync(cancellationToken);
    }
    
    /// <inheritdoc/>
    public async Task<FundingRateHistory?> GetSingleByFilterAsync(
        Expression<Func<FundingRateHistory, bool>> filter, 
        CancellationToken cancellationToken = default)
    {
        return await context.FundingRatesHistory
            .SingleOrDefaultAsync(filter, cancellationToken);
    }
    
    /// <inheritdoc/>
    public async Task<FundingRateHistory?> GetLatestByFilterAsync(
        Expression<Func<FundingRateHistory, bool>> filter, 
        CancellationToken cancellationToken = default)
    {
        return await context.FundingRatesHistory
            .Where(filter)
            .OrderByDescending(f => f.FetchedAt)
            .FirstOrDefaultAsync(cancellationToken);
    }

    /// <inheritdoc/>
    public Task AddAsync(FundingRateHistory fundingRateHistory, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(fundingRateHistory);

        context.FundingRatesHistory.Add(fundingRateHistory);
        
        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public Task AddRangeAsync(IEnumerable<FundingRateHistory> fundingRatesHistory, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(fundingRatesHistory);
        
        context.FundingRatesHistory.AddRangeAsync(fundingRatesHistory, cancellationToken);
        
        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public async Task BulkInsertAsync(IEnumerable<FundingRateHistory> fundingRatesHistory, CancellationToken 
                                          cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(fundingRatesHistory);
        
        var array = fundingRatesHistory as FundingRateHistory[] ?? fundingRatesHistory.ToArray();
        
        await context.BulkInsertAsync(
            array,
            options =>
            {
                options.BatchSize = Math.Min(10_000, array.Length);
                options.UseTempDB = true;
                options.EnableStreaming = true;
                options.BulkCopyTimeout = 0;
            },
            null,
            null,
            cancellationToken);
    }

    /// <inheritdoc/>
    public Task UpdateAsync(FundingRateHistory fundingRateHistory, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(fundingRateHistory);
        
        context.FundingRatesHistory.Update(fundingRateHistory);
        
        return Task.CompletedTask;
    }

    public Task UpdateRangeAsync(IEnumerable<FundingRateHistory> fundingRatesHistory, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(fundingRatesHistory);
        
        context.FundingRatesHistory.UpdateRange(fundingRatesHistory);
        
        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public Task DeleteAsync(FundingRateHistory fundingRateHistory, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(fundingRateHistory);

        context.FundingRatesHistory.Remove(fundingRateHistory);

        return Task.CompletedTask;
    }
}
