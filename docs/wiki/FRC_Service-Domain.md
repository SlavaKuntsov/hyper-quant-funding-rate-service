### FRC_Service.Domain

- [Home](Home.md)
- Components
  - **FRC_Service.Domain** (current)
  - FRC_Service.Infrastructure
    - [Data Access](FRC_Service-Infrastructure-DataAccess.md)
  - [FRC_Service.Application](FRC_Service-Application.md)
  - [FRC_Service.Presentation](FRC_Service-Presentation.md)
  - [FRC_Service](FRC_Service.md)
- [Versioning](Versioning.md)
- [Configuration](Configuration.md)
- [Deployment](Deployment.md)

### Overview

The Domain layer is the core of the FRC_Service application, containing business
entities, enumerations, and repository interfaces. This layer is independent of external
concerns and defines the fundamental business rules and data structures used throughout
the application

## Domain Models

### Exchange

The `Exchange` class represents a trading platform or its market (like Binance,
Bybit, etc.) and stores its code.

``` csharp
public class Exchange
{
    public Guid Id { get; set; }
    public ExchangeCodeType Code { get; set; }
}
```

Properties:

| **Property** | **Type**                              | **Description**                    |
| ------------ |---------------------------------------| ---------------------------------- |
| Id           | Guid                                  | Unique identifier for the exchange |
| Code         | [ExchangeCodeType](#exchangecodetype) | Code of the exchange (e.g., "BIN") |

### FundingRateOnline

The `FundingRateOnline` represents a latest data on Funding Rates for each trading pair on each exchange.

``` csharp
public class FundingRateOnline
{
    public Guid Id { get; set; }
    public Guid ExchangeId { get; set; }
    public string Symbol { get; set; } = null!;
    public string Name { get; set; } = null!;
    public decimal Rate { get; set; }
    public decimal OpenInterest { get; set; }
    public long TsRate { get; set; }
    public long FetchedAt { get; set; } = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
    public Exchange? Exchange { get; set; }
}
```

Properties:

| **Property** | **Type**               | **Description**                                              |
|--------------| ---------------------- |--------------------------------------------------------------|
| Id           | Guid                   | Unique identifier for the exchange                           |
| ExchangeId   | Guid                   | Identifier of the exchange this symbol belongs to            |
| Symbol       | string                 | Trading symbol of the coin pair (e.g., 'BTCUSDT', 'ETHUSDT') |
| Name         | string                 | Name of the coin.                                            |
| Rate         | decimal                | Funding Rate value                                           |
| OpenInterest | decimal                | Open interest (volume of open positions)                     |
| TsRate       | long                   | Timestamp of funding                                         |
| FetchedAt    | long                   | Timestamp                                                    |
| Exchange     | [Exchange](#Exchange)? | Navigation property to the parent exchange                   |

### FundingRateHistory

The `FundingRateHistory` represents a historical data on Funding Rates for each trading pair on each exchange.

``` csharp
public class FundingRateHistory
{
    public Guid Id { get; set; }
    public Guid ExchangeId { get; set; }
    public string Symbol { get; set; } = null!;
    public string Name { get; set; } = null!;
    public byte IntervalHours { get; set; }
    public decimal Rate { get; set; }
    public decimal OpenInterest { get; set; }
    public long TsRate { get; set; }
    public long FetchedAt { get; set; } = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
    public Exchange? Exchange { get; set; }
}
```

Properties:

| **Property**  | **Type**               | **Description**                                              |
|---------------| ---------------------- | ------------------------------------------------------------ |
| Id            | Guid                   | Unique identifier for the exchange                           |
| ExchangeId    | Guid                   | Identifier of the exchange this symbol belongs to            |
| Symbol        | string                 | Trading symbol of the coin pair (e.g., 'BTCUSDT', 'ETHUSDT') |
| Name          | string                 | Name of the coin.                                            |
| IntervalHours | byte                   | The time interval in hours for which the funding is updated  |
| Rate          | decimal                | Funding Rate value                                           |
| OpenInterest  | decimal                | Open interest (volume of open positions)                     |
| TsRate        | long                   | Timestamp of funding                                         |
| FetchedAt     | long                   | Timestamp                                                    |
| Exchange      | [Exchange](#Exchange)? | Navigation property to the parent exchange                   |

## Enumerations

### ExchangeCodeType 

Defines the types of codes for an exchange:

``` csharp
public enum ExchangeCodeType
{
    /// <summary>
    /// Code type for Binance exchange.
    /// </summary>
    Binance,
      
    /// <summary>
    /// Code type for Bybit exchange.
    /// </summary>
    Bybit,
      
    /// <summary>
    /// Code type for HyperLiquid exchange.
    /// </summary>
    HyperLiquid,
      
    /// <summary>
    /// Code type for Mexc exchange.
    /// </summary>
    Mexc
}
```

## Repository Interfaces

The domain layer defines repository interfaces that abstract data access operations. These
interfaces follow the Repository pattern, which isolates domain objects from the details of
database access code. The concrete implementations of these interfaces are provided in the
Infrastructure layer.

### IExchangeRepository

Defines operations for accessing and manipulating `Exchange` entities:

``` csharp
public interface IExchangeRepository
{
    Task<IEnumerable<Exchange>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<IEnumerable<Exchange>> GetByFilterAsync(Expression<Func<Exchange, bool>> filter, CancellationToken cancellationToken = default);
    Task<Exchange?> GetByCodeAsync(ExchangeCodeType code, CancellationToken cancellationToken = default);
    Task AddAsync(Exchange exchange, CancellationToken cancellationToken = default);
    Task UpdateAsync(Exchange exchange, CancellationToken cancellationToken = default);
    Task DeleteAsync(Exchange exchange, CancellationToken cancellationToken = default);
}
```

This interface provides:
- Retrieval of all exchanges with pagination support
- Filtering exchanges based on custom expressions
- Lookup by name for specific exchange retrieval
- Operations for adding, updating, and removing exchanges from the system

The actual data access logic and Entity Framework integration will be implemented in the
`ExchangeRepository` class in the Infrastructure layer.

### IFundingRateHistoryRepository

Defines operations for accessing and manipulating `FundingRateHistory` entities:

``` csharp
public interface IFundingRateHistoryRepository
{
    Task<IEnumerable<FundingRateHistory>> GetLatestSymbolRatesAsync(
        Expression<Func<FundingRateHistory, bool>>? filter = null,
        bool groupByExchange = false,
        int? pageNumber = null,
        int? pageSize = null,
        CancellationToken cancellationToken = default);
    Task<int> GetUniqueSymbolsCountAsync(
        Expression<Func<FundingRateHistory, bool>>? filter = null,
        CancellationToken cancellationToken = default);
    Task<int> GetCountByFilterAsync(
        Expression<Func<FundingRateHistory, bool>> filter, 
        CancellationToken cancellationToken = default);
    Task<IEnumerable<FundingRateHistory>> GetByFilterAsync(
        Expression<Func<FundingRateHistory, bool>> filter, 
        int? pageNumber = null, 
        int? pageSize = null, 
        CancellationToken cancellationToken = default);
    Task<FundingRateHistory?> GetSingleByFilterAsync(
        Expression<Func<FundingRateHistory, bool>> filter, 
        CancellationToken cancellationToken = default);
    Task<FundingRateHistory?> GetLatestByFilterAsync(
        Expression<Func<FundingRateHistory, bool>> filter,
        CancellationToken cancellationToken = default);
    Task AddAsync(FundingRateHistory fundingRateHistory, CancellationToken cancellationToken = default);
    Task AddRangeAsync(IEnumerable<FundingRateHistory> fundingRates, CancellationToken cancellationToken = default);
    Task BulkInsertAsync(IEnumerable<FundingRateHistory> fundingRates, CancellationToken cancellationToken = default);
    Task UpdateAsync(FundingRateHistory fundingRateHistory, CancellationToken cancellationToken = default);
    Task UpdateRangeAsync(IEnumerable<FundingRateHistory> fundingRates, CancellationToken cancellationToken = default);
    Task DeleteAsync(FundingRateHistory fundingRateHistory, CancellationToken cancellationToken = default);
}
```

This interface provides:
- Retrieval of all trading funding rates with pagination support
- Filtering funding rates based on custom expressions
- Specialized method to get all funding rates for a particular exchange
- Lookup by ID for specific symbol retrieval
- Operations for adding, updating, and removing funding rates from the system

The actual implementation will be provided in the `FundingRateHistoryRepository` class in the
Infrastructure layer.

### IFundingRateOnlineRepository

Defines operations for accessing and manipulating `FundingRateOnline` entities:

``` csharp
public interface IFundingRateOnlineRepository
{
    Task<IEnumerable<FundingRateOnline>> GetLatestSymbolFundingRatesAsync(
        int pageNumber = 1,
        int pageSize = 10,
        CancellationToken cancellationToken = default);
    Task<int> GetUniqueSymbolsCountAsync(CancellationToken cancellationToken = default);
    Task<int> GetCountByFilterAsync(
        Expression<Func<FundingRateOnline, bool>> filter, 
        CancellationToken cancellationToken = default);
    Task<IEnumerable<FundingRateOnline>> GetByFilterAsync(
        Expression<Func<FundingRateOnline, bool>> filter, 
        int? pageNumber = null, 
        int? pageSize = null, 
        CancellationToken cancellationToken = default);
    Task<FundingRateOnline?> GetSingleByFilterAsync(
        Expression<Func<FundingRateOnline, bool>> filter, 
        CancellationToken cancellationToken = default);
    Task AddAsync(FundingRateOnline FundingRateOnline, CancellationToken cancellationToken = default);
    Task AddRangeAsync(IEnumerable<FundingRateOnline> fundingRates, CancellationToken cancellationToken = default);
    Task BulkInsertAsync(IEnumerable<FundingRateHistory> fundingRates, CancellationToken cancellationToken = default);
    Task UpdateAsync(FundingRateOnline FundingRateOnline, CancellationToken cancellationToken = default);
    Task UpdateRangeAsync(IEnumerable<FundingRateOnline> fundingRates, CancellationToken cancellationToken = default);
    Task DeleteAsync(FundingRateOnline FundingRateOnline, CancellationToken cancellationToken = default);
}
```

This interface provides:
- Retrieval of all trading funding rates with pagination support
- Filtering funding rates based on custom expressions
- Specialized method to get all funding rates for a particular exchange
- Lookup by ID for specific symbol retrieval
- Operations for adding, updating, and removing funding rates from the system

The actual implementation will be provided in the `FundingRateOnlineRepository` class in the
Infrastructure layer.

### IUnitOfWork

Manages the transaction scope for database operations:

``` csharp
public interface IUnitOfWork
{
    Task SaveAsync(CancellationToken cancellationToken = default);
}
```

The concrete implementation in the Infrastructure layer (typically `UnitOfWork` class) will
handle the actual database transaction management using Entity Framework's
`SaveChangesAsync` method.