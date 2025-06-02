### FRC_Service.Infrastructure.DataAccess

- [Home](Home.md)
- Components
  - [FRC_Service.Domain](FRC_Service-Domain.md)
  - FRC_Service.Infrastructure
    - **Data Access** (current)
  - [FRC_Service.Application](FRC_Service-Application.md)
  - [FRC_Service.Presentation](FRC_Service-Presentation.md)
  - [FRC_Service](FRC_Service.md)
- [Versioning](Versioning.md)
- [Configuration](Configuration.md)
- [Deployment](Deployment.md)

### Overview

The Data Access layer of FRC_Service implements the persistence concerns of the
application. This layer is responsible for:
- Defining the database context and entity configurations
- Implementing repository interfaces defined in the Domain layer
- Managing database connections and transactions
- Providing a clean abstraction for data access operations

## Key Components

### ApplicationDbContext

The `ApplicationDbContext` serves as the central point for database interactions using
Entity Framework Core:

``` csharp
public class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : DbContext(options)
{
    public DbSet<Exchange> Exchanges { get; set; }
    public DbSet<FundingRateHistory> FundingRatesHistory { get; set; }
    public DbSet<FundingRateOnline> FundingRatesOnline { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(AssemblyReference.Assembly);
        base.OnModelCreating(modelBuilder);
    }
}
```

The context is configured to use PostgreSQL as the database provider and applies entity
configurations from the assembly.

## Entity Configurations

Entity configurations define the database schema and constraints for each entity type:

### ExchangeConfiguration

``` csharp
public class ExchangeConfiguration: IEntityTypeConfiguration<Exchange>
{
	public void Configure(EntityTypeBuilder<Exchange> builder)
	{
		builder.HasKey(e => e.Id);
		
		builder.Property(s => s.Code)
			.IsRequired()
			.HasConversion(new EnumToStringConverter<ExchangeCodeType>());
	}
}
```

> [!NOTE] Note
>
> `EnumToStringConverter` is used for enum properties to store them as strings in the database.

### FundingRateHistoryConfiguration

``` csharp
public class FundingRateHistoryConfiguration : IEntityTypeConfiguration<FundingRateHistory>
{
	public void Configure(EntityTypeBuilder<FundingRateHistory> builder)
	{
		builder.HasKey(e => e.Id);

		builder.Property(e => e.ExchangeId)
			.IsRequired();

		builder.Property(e => e.Symbol)
			.IsRequired();

		// other properties

		builder.HasOne(e => e.Exchange)
			.WithMany()
			.HasForeignKey(e => e.ExchangeId)
			.OnDelete(DeleteBehavior.Restrict);

		builder.HasIndex(e => new { e.Symbol, e.ExchangeId, e.TsRate })
			.HasDatabaseName("ix_fr_history")
			.IsDescending(false, false, true);
	}
}
```

### FundingRateHistoryConfiguration

``` csharp
public class FundingRateOnlineConfiguration : IEntityTypeConfiguration<FundingRateOnline>
{
	public void Configure(EntityTypeBuilder<FundingRateOnline> builder)
	{
		builder.HasKey(e => e.Id);

		builder.Property(e => e.Symbol)
			.IsRequired();

		builder.Property(e => e.ExchangeId)
			.IsRequired();

		// other properties
		
		builder.HasIndex(e => new { e.Symbol, e.ExchangeId })
			.IsUnique()
			.HasDatabaseName("ix_fr_online_symbol_exchange_unique");

		builder.HasIndex(e => new { e.Name, e.ExchangeId })
			.IsUnique()
			.HasDatabaseName("ix_fr_online_name_exchange_unique");

		builder.HasIndex(e => e.Symbol)
			.HasDatabaseName("ix_fr_online_symbol");

		builder.HasIndex(e => e.TsRate)
			.HasDatabaseName("ix_fr_online_tsrate")
			.IsDescending(true);
	}
}
```

## Repository Implementation

The Data Access layer provides concrete implementations of the domain repository
interfaces.

### ExchangeRepository

Implements the [IExchangeRepository](FRC_Service-Domain.md#iexchangerepository) interface from the Domain layer:

``` csharp
public class ExchangeRepository(ApplicationDbContext context) : IExchangeRepository
{
    public async Task<IEnumerable<Exchange>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return await context.Exchanges
            .OrderBy(e => e.Code)
            .ToListAsync(cancellationToken: cancellationToken);
    }
    
    // other methods
}
```

### FundingRateHistoryRepository

Implements the [IFundingRateHistoryRepository](FRC_Service-Domain.md#ifundingratehistoryrepository) interface from the Domain layer:

``` csharp
public class FundingRateHistoryRepository(ApplicationDbContext context) : IFundingRateHistoryRepository
{
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
            if (pageNumber.HasValue && pageSize.HasValue)
            {
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
            query = baseQuery
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
    
    // other methods
}
```

### FundingRateOnlineRepository

Implements the [IFundingRateOnlineRepository](FRC_Service-Domain.md#ifundingrateonlinerepository) interface from the Domain layer:

``` csharp
public class FundingRateOnlineRepository(ApplicationDbContext context) : IFundingRateOnlineRepository
{
    public async Task<IEnumerable<FundingRateOnline>> GetLatestSymbolFundingRatesAsync(
        int pageNumber = 1,
        int pageSize = 10,
        CancellationToken cancellationToken = default)
    {
        var symbols = await context.FundingRatesOnline
            .AsNoTracking()
            .Select(f => f.Symbol)
            .Distinct()
            .OrderBy(s => s)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return await context.FundingRatesOnline
            .AsNoTracking()
            .Include(f => f.Exchange)
            .Where(f => symbols.Contains(f.Symbol))
            .GroupBy(f => new { f.Symbol, f.ExchangeId })
            .Select(g => g.OrderByDescending(f => f.TsRate).First())
            .ToListAsync(cancellationToken);
    }
    
    // other methods
}
```

### UnitOfWork

Implements the [IUnitOfWork](FRC_Service-Domain.md#iunitofwork) interface from the Domain layer:

``` csharp
public class UnitOfWork(ApplicationDbContext context) : IUnitOfWork
{
    /// <inheritdoc/>
    public async Task SaveAsync(CancellationToken cancellationToken = default)
    {
        await context.SaveChangesAsync(cancellationToken);
    }
}
```

> [!NOTE] Note
>
> `Unit0fWork.SaveAsync()` should be called after all data modification operations to ensure changes are persisted to the database.

## Database Schema

The Data Access layer generates migrations that create the following PostgreSQL database schema:

## Configuration

### Database Connection

The application uses PostgreSQL as its database, with connection details configured in `appsettings.json`:

```
"ConnectionStrings": {
  "DefaultConnection": "Host=localhost;Database=frcservicedb;Username=postgres;Password=postgres;IncludeErrorDetail=true"
}
```

1. The connection string is retrieved in several places:

``` csharp
options.UseNpgsql(
        configuration.GetConnectionString("DefaultConnection"),
        b => b.MigrationsAssembly(AssemblyReference.Assembly))
    .UseSnakeCaseNamingConvention();
```

2. In ApplicationDbContextDesignTimeFactory for EF Core migrations:

``` csharp
var connectionString = configuration.GetConnectionString("DefaultConnection");
```

### Dependency Registration

All data access services are registered in the DI container through the AddInfrastructure
extension method:

``` csharp
public static IServiceCollection AddInfrastructure(
    this IServiceCollection services,
    IConfiguration configuration,
    IHostEnvironment environment)
{
    services.AddDbContext<ApplicationDbContext>(options =>
    {
        options.UseNpgsql(
                configuration.GetConnectionString("DefaultConnection"),
                b => b.MigrationsAssembly(AssemblyReference.Assembly))
            .UseSnakeCaseNamingConvention();

        // Includes more logging info for debugging purposes.
        if (environment.IsDevelopment())
        {
            options
                .EnableSensitiveDataLogging()
                .EnableDetailedErrors();
        }
    });

    // Register repositories & unit of work implementations.
    services.AddScoped<IExchangeRepository, ExchangeRepository>();
    services.AddScoped<IFundingRateHistoryRepository, FundingRateHistoryRepository>();
    services.AddScoped<IFundingRateOnlineRepository, FundingRateOnlineRepository>();
    services.AddScoped<IUnitOfWork, UnitOfWork>();
    
    // Registers Binance, Bybit, HyperLiquid and Mexc rest and websocket clients to access exchange information.
    services.AddBinance();
    services.AddBybit();
    services.AddHyperLiquid();
    services.AddMexc();
        
    return services;
}
```

By centralizing all data access registrations, the application startup remains clean and
focused on composition rather than implementation details.