using Microsoft.EntityFrameworkCore;
using FRC_Service.Domain.Models;

namespace FRC_Service.Infrastructure.DataAccess;

/// <summary>
/// Represents the application's database context that provides access to the database entities
/// </summary>
/// <param name="options">The configuration options for this context</param>
public class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : DbContext(options)
{
    /// <summary>
    /// Represents the collection of <see cref="Exchange"/> entities in the database.
    /// </summary>
    public DbSet<Exchange> Exchanges { get; set; }
    
    /// <summary>
    /// Represents the collection of <see cref="FundingRateHistory"/> entities in the database.
    /// </summary>
    public DbSet<FundingRateHistory> FundingRatesHistory { get; set; }
    
    /// <summary>
    /// Represents the collection of <see cref="FundingRateOnline"/> entities in the database.
    /// </summary>
    public DbSet<FundingRateOnline> FundingRatesOnline { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(AssemblyReference.Assembly);
        base.OnModelCreating(modelBuilder);
    }
}