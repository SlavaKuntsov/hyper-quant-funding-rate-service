### FRC_Service.Application

- [Home](Home.md)
- Components
  - [FRC_Service.Domain](FRC_Service-Domain.md)
  - FRC_Service.Infrastructure
    - [Data Access](FRC_Service-Infrastructure-DataAccess.md)
  - **FRC_Service.Application** (current)
  - [FRC_Service.Presentation](FRC_Service-Presentation.md)
  - [FRC_Service](FRC_Service.md)
- [Versioning](Versioning.md)
- [Configuration](Configuration.md)
- [Deployment](Deployment.md)

### Overview

The Application layer implements the core business logic of the FRC_Service. This layer
is responsible for:

- Implementing use cases through application services
- Defining Data Transfer Objects (DTOs) for client-server communication
- Validating incoming requests
- Providing error handling through custom exceptions

### Key Components

#### Application Services

Application services implement the business logic for various operations on exchanges and
symbols.

#### IFundingRateHistoryService

Interface defining operations for managing history of funding rates:

``` csharp
public interface IFundingRateHistoryService
{
    Task<IEnumerable<FundingRateDto>> UpdateHistoricalFundingsAsync(CancellationToken cancellationToken = default);
    Task<PaginationDto<List<FundingRateDto>>> GetExchangeLatestFundingRatesAsync(
        ExchangeCodeType exchangeCode,
        int pageNumber = 1,
        int pageSize = 10,
        CancellationToken cancellationToken = default);
    Task<PaginationDto<List<FundingRateDto>>> GetSymbolExchangeHistoryAsync(
        string symbol,
        ExchangeCodeType exchangeCode,
        int pageNumber = 1,
        int pageSize = 10,
        CancellationToken cancellationToken = default);
    Task<PaginationDto<List<SymbolFundingRatesDto>>> GetLatestSymbolFundingRatesAsync(
        int pageNumber = 1,
        int pageSize = 10,
        CancellationToken cancellationToken = default);
}
```

#### BaseFundingRateHistoryService

Implementation of `IFundingRateHistoryService` that:

- Uses repository to access data about historical funding rates
- Provides abstract methods for specific implementations 
for different exchanges
- Is an approximate copy for Background Jobs

``` csharp
public abstract class BaseFundingRateHistoryService<TLogger>(
    ILogger<TLogger> logger,
    IExchangeRepository exchangeRepository,
    IFundingRateHistoryRepository fundingRateHistoryRepository,
    IUnitOfWork unitOfWork)
    : IFundingRateHistoryService where TLogger : class
{
	protected abstract ExchangeCodeType ExchangeCode { get; }
    protected virtual int MaxNumbersOfParallelism => 2;
    protected virtual int BatchSizeForHistory => 10;
    protected virtual int Limit => 1000;
    private SemaphoreSlim? _semaphore;
    private SemaphoreSlim Semaphore => _semaphore ??= new SemaphoreSlim(MaxNumbersOfParallelism);
    
    protected abstract Task<IEnumerable<SymbolPairDto>> FetchSymbolsAsync(CancellationToken cancellationToken = default);
    protected abstract Task<IEnumerable<FuturesDataDto>> FetchAllFundingRatesAsync(string symbolName, DateTime? startTime, CancellationToken cancellationToken = default);
    protected abstract Task<FuturesDataDto?> FetchFundingRateAsync(string symbolName, CancellationToken cancellationToken = default);
    protected abstract ExchangeInfoSymbol GetExchangeInfoSymbol(object symbol);
    protected abstract FundingInfoSymbol GetFundingInfoSymbol(object symbol);
    protected virtual Task LaunchDelayForApi(int count, CancellationToken cancellationToken) => Task.CompletedTask;
    
    /// <inheritdoc />
    public async Task<IEnumerable<FundingRateDto>> UpdateHistoricalFundingsAsync(CancellationToken cancellationToken = default)
    {
        logger.LogInformation(
            "Starting {Exchange} symbols synchronization at {Time}", ExchangeCode, DateTime.UtcNow);

        try
        {
            // Validate exchange exists in database
            var exchange = await exchangeRepository.GetByCodeAsync(ExchangeCode, cancellationToken);
            if (exchange == null)
            {
                logger.LogWarning("{Exchange} exchange not found in the database.", ExchangeCode);
                return [];
            }
            
            // Get existing symbols from database
            var allExistingFundingRates = await fundingRateHistoryRepository.GetLatestSymbolRatesAsync(
                f => f.ExchangeId == exchange.Id,
                cancellationToken: cancellationToken);

            var currentTimeMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            
            // Get all symbols from external API
            var symbols = await FetchSymbolsAsync(cancellationToken);
            
            // First time sync - fetch all historical data
            if (!allExistingFundingRates.Any())
            {
                await ProcessAllHistoricalData(
                    symbols,
                    exchange.Id,
                    currentTimeMs,
                    cancellationToken);

                return [];
            }
            
            // Regular sync - fetch only latest updates
            var fundingResultDtos = new ConcurrentQueue<FundingRateDto>();
            
            await ProcessLatestHistoricalData(
                symbols,
                allExistingFundingRates,
                fundingResultDtos,
                currentTimeMs,
                exchange.Id,
                cancellationToken);

            return fundingResultDtos;
        }
        catch (ExchangeApiException ex)
        {
            logger.LogError(ex, "Exchange API error during {Exchange} symbol synchronization: {Message}", 
                ExchangeCode, ex.Message);
        }
        catch (DatabaseException ex)
        {
            logger.LogError(ex, "Database error during {Exchange} symbol synchronization: {Message}", 
                ExchangeCode, ex.Message);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unexpected error during {Exchange} symbol synchronization: {Message}", 
                ExchangeCode, ex.Message);
        }
        
        return [];
    }
    
    // other methods
```

#### IFundingRateOnlineService

Interface defining operations for managing latest updates of funding rates:

``` csharp
public interface IFundingRateOnlineService
{
    Task<IEnumerable<FundingRateDto>> UpdateOnlineFundingsAsync(CancellationToken cancellationToken = default);
}
```

#### BaseFundingRateOnlineService

Implementation of `IFundingRateOnlineService` that:

- Uses repository to access data about latest funding rates
- Provides abstract methods for specific implementations
  for different exchanges
- Is an approximate copy for Background Jobs

``` csharp
public abstract class BaseFundingRateOnlineService<TLogger>(
	ILogger<TLogger> logger,
	IExchangeRepository exchangeRepository,
	IFundingRateOnlineRepository fundingRateOnlineRepository,
	IUnitOfWork unitOfWork)
	: IFundingRateOnlineService where TLogger : class
{
	protected abstract ExchangeCodeType ExchangeCode { get; }
	protected virtual int MaxNumbersOfParallelism => 2;
	private SemaphoreSlim? _semaphore;
	private SemaphoreSlim Semaphore => _semaphore ??= new SemaphoreSlim(MaxNumbersOfParallelism);

	protected abstract Task<IEnumerable<object>> FetchSymbolsAsync(CancellationToken cancellationToken = default);
	protected abstract Task<FuturesDataDto?> FetchFuturesAsync(string symbolName, CancellationToken cancellationToken = default);
	protected abstract string GetSymbolName(object symbol);
	
	/// <inheritdoc/>
	public async Task<PaginationDto<List<FundingRateDto>>> GetExchangeFundingRatesAsync(
	    ExchangeCodeType exchangeCode,
	    int pageNumber = 1,
	    int pageSize = 10,
	    CancellationToken cancellationToken = default)
	{
        logger.LogInformation("Getting exchange funding rates for {Exchange}. Page: {Page}, Size: {Size}", 
            exchangeCode, pageNumber, pageSize);

        // Validate exchange exists
        var exchange = await exchangeRepository.GetByCodeAsync(exchangeCode, cancellationToken);
        if (exchange == null)
        {
            logger.LogWarning("Exchange {ExchangeCode} not found", exchangeCode);
            throw new NotFoundException($"Exchange {exchangeCode} not found");
        }

        // Get paginated funding rates for this exchange
        var fundingRates = await fundingRateOnlineRepository
            .GetByFilterAsync(
                f => f.ExchangeId == exchange.Id,
                pageNumber,
                pageSize,
                cancellationToken);

        // Get total count for pagination
        var totalCount = await fundingRateOnlineRepository
            .GetCountByFilterAsync(
                f => f.ExchangeId == exchange.Id, 
                cancellationToken);

        // Convert domain entities to DTOs
        var data = fundingRates
			.Select(rate => new FundingRateDto(
				rate.Symbol,
				rate.Rate,
				rate.TsRate,
				rate.FetchedAt))
			.OrderBy(dto => dto.Symbol) // Sort by symbol for consistent output
			.ToList();

        // Calculate pagination metadata
        var totalPages = (int)Math.Ceiling(totalCount / (double)pageSize);

        logger.LogInformation("Successfully retrieved {Count} funding rates for {Exchange}", 
            data.Count, exchangeCode);

        return new PaginationDto<List<FundingRateDto>>(
            data,
            pageNumber,
            pageSize,
            totalCount,
            totalPages);
	}
	
	// other methods
}
```

### Data Transfer Objects (DTOs)

DTOs are used to transfer data between the Application layer and the Presentation layer.

#### ExchangeInfoSymbol

Used to get data from object

``` csharp
public record ExchangeInfoSymbol(
	string SymbolName,
	DateTime? ListingDate);
```

#### FundingInfoSymbol

Used to get data from object

``` csharp
public record FundingInfoSymbol(
	string SymbolName,
	int? FundingIntervalHours,
	DateTime? LaunchTime);
```

#### FundingRateDto, ExchangeFundingRateDto

Represents the core funding rate data for a trading pair

``` csharp
public record FundingRateDto(
	string Symbol,
	decimal Rate,
	long TsRate,
	long FetchedAt);

public record ExchangeFundingRateDto(
	ExchangeCodeType ExchangeName,
	string Symbol,
	decimal Rate,
	long TsRate,
	long FetchedAt) : FundingRateDto(Symbol, Rate, TsRate, FetchedAt);
```

#### FuturesDataDto

Used for convenient manipulation data in services

``` csharp

public sealed record FuturesDataDto(
    decimal FundingRate,
    DateTime FundingTime,
    int? FundingIntervalHours = null);
```

#### FundingResultDto

User to return data from controller

``` csharp
public record FundingResultDto(
	int Count,
	IEnumerable<FundingRateDto> FundingRates);
```

#### PaginationDto

Used to represent data with pagination

``` csharp
public record PaginationDto<T>(
	T Data,
	int CurrentPage,
	int PageSize,
	int TotalRecords,
	int TotalPages);
```

#### SymbolFundingRatesDto

Aggregates funding rate information for a specific trading pair across multiple exchanges

``` csharp
public record SymbolFundingRatesDto(
	string Symbol,
	List<ExchangeFundingRateDto> Exchanges);
```

#### SymbolPairDto

Used to represent data from two lists - ExchangeSymbol and FundingSymbols

``` csharp
public record SymbolPairDto(
	object? ExchangeSymbol, 
	object? FundingSymbols);
```

### Dependency Registration

Provides an extension method for registering its services:

``` csharp
public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        // Register history services
        services.AddScoped<BinanceFundingRateHistoryService>();
        services.AddScoped<BybitFundingRateHistoryService>();
        services.AddScoped<HyperLiquidFundingRateHistoryService>();
        services.AddScoped<MexcFundingRateHistoryService>();

        services.AddScoped<IFundingRateHistoryService>(provider => 
            provider.GetRequiredService<BinanceFundingRateHistoryService>());

        services.AddScoped<IFundingRateHistoryService>(provider => 
            provider.GetRequiredService<BybitFundingRateHistoryService>());

        services.AddScoped<IFundingRateHistoryService>(provider => 
            provider.GetRequiredService<HyperLiquidFundingRateHistoryService>());

        services.AddScoped<IFundingRateHistoryService>(provider => 
            provider.GetRequiredService<MexcFundingRateHistoryService>());

        // Register online services
        services.AddScoped<BinanceFundingRateOnlineService>();
        services.AddScoped<BybitFundingRateOnlineService>();
        services.AddScoped<HyperLiquidFundingRateOnlineService>();
        services.AddScoped<MexcFundingRateOnlineService>();

        services.AddScoped<IFundingRateOnlineService>(provider => 
            provider.GetRequiredService<BinanceFundingRateOnlineService>());

        services.AddScoped<IFundingRateOnlineService>(provider => 
            provider.GetRequiredService<BybitFundingRateOnlineService>());

        services.AddScoped<IFundingRateOnlineService>(provider => 
            provider.GetRequiredService<HyperLiquidFundingRateOnlineService>());

        services.AddScoped<IFundingRateOnlineService>(provider => 
            provider.GetRequiredService<MexcFundingRateOnlineService>());
        
        // Configures validation.  
        services.AddValidatorsFromAssembly(AssemblyReference.Assembly);
        services.AddProblemDetails();
        
        return services;
    }
}
```

### Application Layer Workflow

1. Service Processing: Application services process validated requests
2. Data Access: Services use domain repositories to perform data operations
3. Domain Entity Mapping: Results are mapped from domain entities to DTOs
4. Response Return: DTOs are returned to the Presentation layer