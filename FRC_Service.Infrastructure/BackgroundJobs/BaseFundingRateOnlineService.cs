using System.Collections.Concurrent;
using FRC_Service.Domain.Enums;
using FRC_Service.Domain.Models;
using FRC_Service.Domain.Repositories;
using FRC_Service.Infrastructure.Exceptions;
using Microsoft.Extensions.Logging;
using Quartz;

namespace FRC_Service.Infrastructure.BackgroundJobs;

public abstract class BaseFundingRateOnlineSyncJob<TLogger>(
	ILogger<TLogger> logger,
	IExchangeRepository exchangeRepository,
	IFundingRateOnlineRepository fundingRateOnlineRepository,
	IUnitOfWork unitOfWork)
	: IJob where TLogger : class
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
	/// Returns tuple containing funding rate, funding time, and optional interval hours.
	/// </summary>
	protected abstract Task<(decimal FundingRate, DateTime FundingTime, int? FundingIntervalHours)?> FetchFuturesAsync(string symbolName, CancellationToken cancellationToken = default);
	/// <summary>
	/// Extracts symbol name from object.
	/// </summary>
	protected abstract string GetSymbolName(object symbol);
	
	
	/// <inheritdoc />
	public async Task Execute(IJobExecutionContext context)
	{
		logger.LogInformation(
			"Starting {Exchange} symbols synchronization at {Time}", ExchangeCode, DateTime.UtcNow);

		try
		{
			// Validate exchange exists in database
			var exchange = await exchangeRepository.GetByCodeAsync(ExchangeCode);
			if (exchange == null)
			{
				logger.LogWarning("{Exchange} exchange not found in the database.", ExchangeCode);
				return;
			}

			// Fetch active symbols from exchange
			var symbols = await FetchSymbolsAsync();
			var symbolsList = symbols.ToList();

			// Prepare collections for results
			var fundingResultDtos = new ConcurrentBag<(string Symbol, decimal FundingRate, long TsRate, long FetchedAt)>();
			var updatedFundingResult = new ConcurrentBag<FundingRateOnline>();
			var createdFundingResult = new ConcurrentBag<FundingRateOnline>();

			var allExistingFundingRates = await fundingRateOnlineRepository.GetByFilterAsync(
				f => f.ExchangeId == exchange.Id);
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
				fundingResultDtos);

			logger.LogInformation(
				"Completed processing. Total: {Total}, Created: {Created}, Updated: {Updated}",
				symbolsList.Count,
				createdFundingResult.Count,
				updatedFundingResult.Count);

			await SaveSymbolsAsync(updatedFundingResult, createdFundingResult);
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
		ConcurrentBag<(string Symbol, decimal FundingRate, long TsRate, long FetchedAt)> fundingResultDtos,
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
		ConcurrentBag<(string Symbol, decimal FundingRate, long TsRate, long FetchedAt)> fundingResultDtos,
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
				newFundingRate.Value,
				exchange.Id,
				currentTimeMs,
				allExistingFundingRatesDict,
				createdFundingResult,
				updatedFundingResult);

			// Create DTO for return
			var fundingRateDto = (
				symbolName,
				newFundingRate.Value.FundingRate,
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
	private async Task<(decimal FundingRate, DateTime FundingTime, int? FundingIntervalHours)?> FetchFundingRateWithRetry(
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
		(decimal FundingRate, DateTime FundingTime, int? FundingIntervalHours) fundingData,
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