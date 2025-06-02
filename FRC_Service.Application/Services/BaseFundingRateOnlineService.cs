using System.Collections.Concurrent;
using FRC_Service.Application.Abstractions;
using FRC_Service.Application.Dtos;
using FRC_Service.Application.Exceptions;
using FRC_Service.Domain.Enums;
using FRC_Service.Domain.Models;
using FRC_Service.Domain.Repositories;
using FRC_Service.Infrastructure.Exceptions;
using Microsoft.Extensions.Logging;

namespace FRC_Service.Application.Services;

/// <inheritdoc/>
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

	/// <summary>
	/// Fetches active perpetual symbols that have funding rates.
	/// </summary>
	protected abstract Task<IEnumerable<object>> FetchSymbolsAsync(CancellationToken cancellationToken = default);
	/// <summary>
	/// Fetches the latest funding rate for a specific symbol.
	/// </summary>
	protected abstract Task<FuturesDataDto?> FetchFuturesAsync(string symbolName, CancellationToken cancellationToken = default);
	/// <summary>
	/// Extracts symbol name from object.
	/// </summary>
	protected abstract string GetSymbolName(object symbol);
	
	/// <inheritdoc/>
	public async Task<PaginationDto<List<SymbolFundingRatesDto>>> GetSymbolsFundingRatesAsync(
		int pageNumber = 1,
		int pageSize = 10,
		CancellationToken cancellationToken = default)
	{
        logger.LogInformation("Getting symbol funding rates. Page: {Page}, Size: {Size}", 
            pageNumber, pageSize);

        // Call first repository method - get flat list of entities
        var fundingEntities = await fundingRateOnlineRepository
            .GetLatestSymbolFundingRatesAsync(pageNumber, pageSize, cancellationToken);

        // Call second repository method - get total count
        var totalSymbols = await fundingRateOnlineRepository
            .GetUniqueSymbolsCountAsync(cancellationToken);

        // Grouping by symbol
        var groupedBySymbol = fundingEntities
			.GroupBy(f => f.Symbol)
			.ToDictionary(g => g.Key, g => g.ToList());;

        // Service converts domain entities to DTOs
        var symbolFundingData = ConvertToSymbolFundingRatesDto(groupedBySymbol);

		var totalPages = (int)Math.Ceiling(totalSymbols / (double)pageSize);
    
        logger.LogInformation("Successfully retrieved {Count} symbols", symbolFundingData.Count);
		
		return new PaginationDto<List<SymbolFundingRatesDto>>(
			symbolFundingData,
			pageNumber,
			pageSize,
			totalSymbols,
			totalPages);
    }
	
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

	/// <inheritdoc />
	public async Task<IEnumerable<FundingRateDto>> UpdateOnlineFundingsAsync(CancellationToken cancellationToken = default)
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

			// Fetch active symbols from exchange
			var symbols = await FetchSymbolsAsync(cancellationToken);
			var symbolsList = symbols.ToList();

			// Prepare collections for results
			var fundingResultDtos = new ConcurrentBag<FundingRateDto>();
			var updatedFundingResult = new ConcurrentBag<FundingRateOnline>();
			var createdFundingResult = new ConcurrentBag<FundingRateOnline>();

			var allExistingFundingRates = await fundingRateOnlineRepository.GetByFilterAsync(
				f => f.ExchangeId == exchange.Id,
				cancellationToken: cancellationToken);
			var allExistingFundingRatesDict = allExistingFundingRates
				.ToDictionary(s => s.Symbol);

			var currentTimeMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

			await ProcessAllSymbols(
				symbolsList,
				exchange,
				currentTimeMs,
				allExistingFundingRatesDict,
				createdFundingResult,
				updatedFundingResult,
				fundingResultDtos,
				cancellationToken);

			logger.LogInformation(
				"Completed processing. Total: {Total}, Created: {Created}, Updated: {Updated}",
				symbolsList.Count,
				createdFundingResult.Count,
				updatedFundingResult.Count);

			await SaveSymbolsAsync(updatedFundingResult, createdFundingResult, cancellationToken);

			return fundingResultDtos;
		}
		catch (ExchangeApiException ex)
		{
			logger.LogError(
				ex,
				"Exchange API error during {Exchange} symbol synchronization: {Message}",
				ExchangeCode,
				ex.Message);
		}
		catch (DatabaseException ex)
		{
			logger.LogError(
				ex,
				"Database error during {Exchange} symbol synchronization: {Message}",
				ExchangeCode,
				ex.Message);
		}
		catch (Exception ex)
		{
			logger.LogError(
				ex,
				"Unexpected error during {Exchange} symbol synchronization: {Message}",
				ExchangeCode,
				ex.Message);
		}
		finally
		{
			// Force garbage collection after processing large amounts of data
			GC.Collect(0, GCCollectionMode.Optimized);
		}

		return [];
	}

    /// <summary>
    /// Converts grouped domain entities to DTOs
    /// </summary>
	private static List<SymbolFundingRatesDto> ConvertToSymbolFundingRatesDto(
		Dictionary<string, List<FundingRateOnline>> groupedEntities)
	{
		var result = new List<SymbolFundingRatesDto>();

		foreach (var (symbol, rates) in groupedEntities)
		{
			var exchanges = new List<ExchangeFundingRateDto>();

			foreach (var rate in rates)
			{
				exchanges.Add(new ExchangeFundingRateDto(
					rate.Exchange.Code,
					rate.Symbol,    // ← Нужно передать все параметры базового record
					rate.Rate,
					rate.TsRate,
					rate.FetchedAt));
			}

			exchanges = exchanges.OrderBy(e => e.ExchangeName).ToList();
			result.Add(new SymbolFundingRatesDto(symbol, exchanges));
		}

		return result.OrderBy(s => s.Symbol).ToList();
	}
	
	/// <summary>
	/// Processes all symbols in parallel with controlled concurrency
	/// </summary>
	private async Task ProcessAllSymbols(
		List<object> symbolsList,
		Exchange exchange,
		long currentTimeMs,
		Dictionary<string, FundingRateOnline> allExistingFundingRatesDict,
		ConcurrentBag<FundingRateOnline> createdFundingResult,
		ConcurrentBag<FundingRateOnline> updatedFundingResult,
		ConcurrentBag<FundingRateDto> fundingResultDtos,
		CancellationToken cancellationToken = default)
	{
		var tasks = new List<Task>(symbolsList.Count);

		foreach (var symbolObject in symbolsList)
		{
			tasks.Add(ProcessSymbolAsync(
				symbolObject,
				exchange,
				currentTimeMs,
				allExistingFundingRatesDict,
				createdFundingResult,
				updatedFundingResult,
				fundingResultDtos,
				cancellationToken));
		}

		await Task.WhenAll(tasks);
	}

	/// <summary>
	/// Create and add latest funding rate item 
	/// </summary>
	private async Task ProcessSymbolAsync(
		object symbolObject,
		Exchange exchange,
		long currentTimeMs,
		Dictionary<string, FundingRateOnline> allExistingFundingRatesDict,
		ConcurrentBag<FundingRateOnline> createdFundingResult,
		ConcurrentBag<FundingRateOnline> updatedFundingResult,
		ConcurrentBag<FundingRateDto> fundingResultDtos,
		CancellationToken cancellationToken = default)
	{
		await Semaphore.WaitAsync(cancellationToken);

		try
		{
			var symbolName = GetSymbolName(symbolObject);

			var newFundingRate = await FetchFundingRateWithRetry(symbolName, cancellationToken);
			
			if (newFundingRate == null)
			{
				logger.LogWarning("Failed to fetch funding rate for symbol {Symbol}", symbolName);
				return;
			}

			// Create or update funding rate entity
			var fundingRate = CreateOrUpdateFundingRate(
				symbolName,
				newFundingRate,
				exchange.Id,
				currentTimeMs,
				allExistingFundingRatesDict,
				createdFundingResult,
				updatedFundingResult);

			// Create DTO for return
			var fundingRateDto = new FundingRateDto(
				symbolName,
				newFundingRate.FundingRate,
				fundingRate.TsRate,
				currentTimeMs);

			fundingResultDtos.Add(fundingRateDto);
		}
		catch (Exception ex)
		{
			logger.LogWarning(ex, "Error processing symbol {Symbol}", symbolObject);
		}
		finally
		{
			Semaphore.Release();
		}
	}
	
	/// <summary>
	/// Fetches funding rate with retry logic for handling transient failures.
	/// </summary>
	private async Task<FuturesDataDto?> FetchFundingRateWithRetry(
		string symbolName,
		CancellationToken cancellationToken)
	{
		return await ExecuteWithRetry(
			() => FetchFuturesAsync(symbolName, cancellationToken),
			symbolName,
			cancellationToken);
	}
	
	/// <summary>
	/// Creates a new funding rate entity or updates an existing one.
	/// </summary>
	private FundingRateOnline CreateOrUpdateFundingRate(
		string symbolName,
		FuturesDataDto fundingData,
		Guid exchangeId,
		long currentTimeMs,
		Dictionary<string, FundingRateOnline> existingRatesDict,
		ConcurrentBag<FundingRateOnline> createdFundingResult,
		ConcurrentBag<FundingRateOnline> updatedFundingResult)
	{
		// Validate funding time
		if (fundingData.FundingTime == default)
		{
			throw new InvalidOperationException($"Invalid funding time for symbol {symbolName}");
		}
		
		var fundingTimeUnix = ((DateTimeOffset)fundingData.FundingTime).ToUnixTimeMilliseconds();

		// Check if we're updating an existing rate or creating a new one
		if (existingRatesDict.TryGetValue(symbolName, out var existingFundingRate))
		{
			// Update existing rate
			var fundingRate = new FundingRateOnline
			{
				Id = existingFundingRate.Id,
				ExchangeId = exchangeId,
				Symbol = NormalizeSymbol(symbolName),
				Name = symbolName,
				Rate = fundingData.FundingRate,
				TsRate = fundingTimeUnix,
				FetchedAt = currentTimeMs
			};
			
			updatedFundingResult.Add(fundingRate);
			return fundingRate;
		}
		else
		{
			// Create new rate
			var fundingRate = new FundingRateOnline
			{
				Id = Guid.NewGuid(),
				ExchangeId = exchangeId,
				Symbol = NormalizeSymbol(symbolName),
				Name = symbolName,
				Rate = fundingData.FundingRate,
				TsRate = fundingTimeUnix,
				FetchedAt = currentTimeMs
			};
			
			createdFundingResult.Add(fundingRate);
			return fundingRate;
		}
	}
	
	/// <summary>
	/// Normalize symbol name
	/// </summary>
	private static string NormalizeSymbol(string symbol)
	{
		return symbol.Replace("_", "").Replace("-", "").ToUpperInvariant();
	}

	/// <summary>
	/// Saves changes to the database
	/// </summary>
	private async Task SaveSymbolsAsync(
		ConcurrentBag<FundingRateOnline> updatedFundingResult,
		ConcurrentBag<FundingRateOnline> createdFundingResult,
		CancellationToken cancellationToken = default)
	{
		// Skip if nothing to save
		if (createdFundingResult.IsEmpty && updatedFundingResult.IsEmpty)
		{
			return;
		}

		try
		{
			// Update existing rates
			if (!updatedFundingResult.IsEmpty)
			{
				await fundingRateOnlineRepository.UpdateRangeAsync(updatedFundingResult, cancellationToken);
			}

			// Create new rates
			if (!createdFundingResult.IsEmpty)
			{
				await fundingRateOnlineRepository.AddRangeAsync(createdFundingResult, cancellationToken);
			}

			await unitOfWork.SaveAsync(cancellationToken);
			
			logger.LogInformation(
				"Successfully saved funding rates. Created: {Created}, Updated: {Updated}",
				createdFundingResult.Count,
				updatedFundingResult.Count);
		}
		catch (Exception ex)
		{
			logger.LogError(ex, 
				"Failed to save funding rates. Created: {Created}, Updated: {Updated}",
				createdFundingResult.Count,
				updatedFundingResult.Count);
			
			throw new DatabaseException(
				"SymbolSyncJob", $"Error updating symbols for {ExchangeCode}", ex);
		}
	}
    
	/// <summary>
	/// Execute an operation with retry logic for handling transient failures.
	/// </summary>
	private async Task<T> ExecuteWithRetry<T>(
		Func<Task<T>> operation,
		string context,
		CancellationToken cancellationToken)
	{
		var attempts = 0;
		const int maxRetryAttempts = 3;
		var retryDelay = TimeSpan.FromSeconds(1);
        
		while (attempts < maxRetryAttempts)
		{
			try
			{
				return await operation();
			}
			catch (Exception ex) when (attempts < maxRetryAttempts - 1)
			{
				attempts++;
				logger.LogWarning(ex, 
					"Operation failed for {Context}, attempt {Attempt}/{MaxAttempts}. Retrying...", 
					context, attempts, maxRetryAttempts);
                
				await Task.Delay(retryDelay * attempts, cancellationToken);
			}
		}
        
		// Final attempt without catching
		return await operation();
	}
}