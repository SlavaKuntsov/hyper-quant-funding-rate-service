using System.Collections.Concurrent;
using FRC_Service.Domain.Enums;
using FRC_Service.Domain.Models;
using FRC_Service.Domain.Repositories;
using FRC_Service.Infrastructure.Exceptions;
using Microsoft.Extensions.Logging;
using Quartz;

namespace FRC_Service.Infrastructure.BackgroundJobs;

public abstract class BaseFundingRateHistorySyncJob<TLogger>(
    ILogger<TLogger> logger,
    IExchangeRepository exchangeRepository,
    IFundingRateHistoryRepository fundingRateHistoryRepository,
    IUnitOfWork unitOfWork)
    : IJob where TLogger : class
{
    protected abstract ExchangeCodeType ExchangeCode { get; }
    protected virtual int MaxNumbersOfParallelism => 2;
    protected virtual int BatchSizeForHistory => 10;
    protected virtual int Limit => 1000;
    private SemaphoreSlim? _semaphore;
    private SemaphoreSlim Semaphore => _semaphore ??= new SemaphoreSlim(MaxNumbersOfParallelism);

    /// <summary>
    /// Fetches active perpetual symbols from exchange that have funding rates.
    /// Returns tuples containing exchange symbol and funding symbol objects.
    /// </summary>	
    protected abstract Task<IEnumerable<(object? ExchangeSymbol, object? FundingSymbol)>> FetchSymbolsAsync(CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Fetches all historical funding rates for a symbol starting from a specific time.
    /// Returns tuples containing funding rate, funding time, and optional interval hours.
    /// </summary>
    protected abstract Task<IEnumerable<(decimal FundingRate, DateTime FundingTime, int? FundingIntervalHours)>> FetchAllFundingRatesAsync(string symbolName, DateTime? startTime, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Fetches the latest funding rate for a specific symbol.
    /// Returns tuple containing funding rate, funding time, and optional interval hours.
    /// </summary>
    protected abstract Task<(decimal FundingRate, DateTime FundingTime, int? FundingIntervalHours)?> FetchFundingRateAsync(string symbolName, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Extracts symbol name and listing date from exchange symbol object.
    /// </summary>
    protected abstract (string SymbolName, DateTime? ListingDate) GetExchangeSymbolInfo(object symbol);
    
    /// <summary>
    /// Extracts symbol name, funding interval hours, and launch time from funding symbol object.
    /// </summary>
    protected abstract (string SymbolName, int? FundingIntervalHours, DateTime? LaunchTime) GetFundingSymbolInfo(object symbol);
    
    /// <summary>
    /// Implements dynamic delay between batches based on the number of API calls made.
    /// Helps to avoid hitting API rate limits during large historical data fetches.
    /// </summary>
    protected virtual Task LaunchDelayForApi(int count, CancellationToken cancellationToken) => Task.CompletedTask;
    
    /// <summary>
    /// Adds a new fundings.
    /// </summary>
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
            
            // Get existing symbols from database
            var allExistingFundingRates = await fundingRateHistoryRepository.GetLatestSymbolRatesAsync(
                f => f.ExchangeId == exchange.Id);

            var currentTimeMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            
            // Get all symbols from external API
            var symbols = await FetchSymbolsAsync();
            
            // First time sync - fetch all historical data
            if (!allExistingFundingRates.Any())
            {
                await ProcessAllHistoricalData(
                    symbols,
                    exchange.Id,
                    currentTimeMs);

                return;
            }
            
            // Regular sync - fetch only latest updates
            await ProcessLatestHistoricalData(
                symbols,
                allExistingFundingRates,
                currentTimeMs,
                exchange.Id);
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
    }
    
    /// <summary>
    /// Process latest historical data and save to database.
    /// </summary>
    private async Task ProcessLatestHistoricalData(
        IEnumerable<(object? ExchangeSymbol, object? FundingSymbol)> symbols,
        IEnumerable<FundingRateHistory> allExistingFundingRates,
        long currentTimeMs,
        Guid exchangeId,
        CancellationToken cancellationToken = default)
    {
        var fundingResult = new ConcurrentQueue<FundingRateHistory>();
        
        var existingRatesDict = allExistingFundingRates.ToDictionary(
            f => f.Name, 
            StringComparer.OrdinalIgnoreCase);
        var symbolsList = symbols.ToList();
        
        // Process all symbols in parallel
        var tasks = new List<Task>(symbolsList.Count);
        
        foreach (var symbolPair in symbolsList)
        {
            tasks.Add(ProcessLatestSymbolAsync(
                currentTimeMs, 
                exchangeId, 
                Semaphore,
                symbolPair,
                existingRatesDict,
                fundingResult,
                cancellationToken));
        }

        await Task.WhenAll(tasks);
        
        logger.LogInformation("Completed {Service} at {Now}. Symbols processed: {Total}. Symbols updated: {Updated}",
            nameof(BaseFundingRateHistorySyncJob<ILogger>),
            DateTime.UtcNow,
            symbolsList.Count,
            fundingResult.Count);

        await SaveSymbolsAsync(fundingResult, cancellationToken);
        
        // Force garbage collection after processing large amounts of data
        GC.Collect(0, GCCollectionMode.Optimized);
    }
    
    /// <summary>
    /// Process latest data for a single symbol
    /// </summary>
    private async Task ProcessLatestSymbolAsync(
        long currentTimeMs,
        Guid exchangeId,
        SemaphoreSlim semaphore,
        (object? ExchangeSymbol, object? FundingSymbol) symbolPair,
        Dictionary<string, FundingRateHistory> existingRatesDict,
        ConcurrentQueue<FundingRateHistory> fundingResult,
        CancellationToken cancellationToken = default)
    {
        await semaphore.WaitAsync(cancellationToken);

        try
        {
            if (symbolPair.FundingSymbol == null)
            {
                logger.LogWarning("Funding symbol is null for symbol pair");
                return;
            }

            var fundingSymbolInfo = GetFundingSymbolInfo(symbolPair.FundingSymbol);

            // Check if symbol needs update
            if (existingRatesDict.TryGetValue(fundingSymbolInfo.SymbolName, out var existFundingRate))
            {
                var intervalMs = existFundingRate.IntervalHours * 60 * 60 * 1000;
                var futureFundingRate = existFundingRate.TsRate + intervalMs;

                // Skip if the last funding rate is smaller than the funding interval
                if (futureFundingRate > currentTimeMs)
                {
                    return;
                }
                // Process any missed funding rates if skip some intervals in the past
                else if (currentTimeMs - (2 * intervalMs) > existFundingRate.TsRate)
                {
                    await ProcessMissedFundingRates(
                        existFundingRate,
                        currentTimeMs,
                        exchangeId,
                        fundingSymbolInfo,
                        fundingResult,
                        cancellationToken);
                }
                // Process the latest funding rate
                else
                {
                    await ProcessLatestFundingRate(
                        currentTimeMs,
                        exchangeId,
                        fundingResult,
                        fundingSymbolInfo,
                        cancellationToken);
                }
            }
            else
            {
                // Process all historical data for a non-existent symbol
                var historicalFundings = new ConcurrentBag<FundingRateHistory>();
                
                await ProcessHistorySymbolAsync(
                    exchangeId,
                    currentTimeMs,
                    historicalFundings,
                    null,
                    symbolPair,
                    cancellationToken);
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Error processing symbol {Symbol}", symbolPair);
        }
        finally
        {
            semaphore.Release();
        }
    }
    
    /// <summary>
    /// Process any funding rates that were missed since the last update.
    /// </summary>
    private async Task ProcessMissedFundingRates(
        FundingRateHistory existingRate,
        long currentTimeMs,
        Guid exchangeId,
        (string SymbolName, int? FundingIntervalHours, DateTime? LaunchTime) fundingSymbolInfo,
        ConcurrentQueue<FundingRateHistory> fundingResult,
        CancellationToken cancellationToken)
    {
        // Calculate the start time for fetching missed rates
        var lastFundingRate = DateTimeOffset.FromUnixTimeMilliseconds(existingRate.TsRate).DateTime;
        lastFundingRate = lastFundingRate.AddMilliseconds(1);

        // Fetch all missed funding rates with retry logic
        var fundingRateGaps = await ExecuteWithRetry(
            () => FetchAllFundingRatesAsync(
                fundingSymbolInfo.SymbolName,
                lastFundingRate,
                cancellationToken),
            cancellationToken);

        // Process each missed funding rate
        foreach (var newFundingRateGap in fundingRateGaps)
        {
            CreateFundingRate(
                currentTimeMs,
                exchangeId,
                fundingResult,
                newFundingRateGap,
                fundingSymbolInfo);
        }
    }
    
    /// <summary>
    /// Process the latest funding rate for a symbol.
    /// </summary>
    private async Task ProcessLatestFundingRate(
        long currentTimeMs,
        Guid exchangeId,
        ConcurrentQueue<FundingRateHistory> fundingResult,
        (string SymbolName, int? FundingIntervalHours, DateTime? LaunchTime) fundingSymbolInfo,
        CancellationToken cancellationToken = default)
    {
        // Fetch the latest funding rate with retry logic
        var newFundingRate = await ExecuteWithRetry(
            () => FetchFundingRateAsync(fundingSymbolInfo.SymbolName, cancellationToken),
            cancellationToken);
        
        if (newFundingRate != null)
        {
            CreateFundingRate(
                currentTimeMs,
                exchangeId,
                fundingResult,
                newFundingRate.Value,
                fundingSymbolInfo);
        }
        else
        {
            logger.LogWarning("Failed to fetch latest funding rate for symbol {Symbol}", fundingSymbolInfo.SymbolName);
        }
    }

    /// <summary>
    /// Process all historical data for all time and save to database.
    /// </summary>
    private async Task ProcessAllHistoricalData(
        IEnumerable<(object? ExchangeSymbol, object? FundingSymbol)> exchangeInfo,
        Guid exchangeId,
        long currentTimeMs,
        CancellationToken cancellationToken = default)
    {
        var exchangeInfoList = exchangeInfo.ToList();
        var totalCount = exchangeInfoList.Count;
        
        logger.LogInformation("Starting to process {Count} symbols for full historical data", totalCount);
       
        // Process symbols in batches to manage memory and API rate limits
        for (var batchIndex = 0; batchIndex < totalCount; batchIndex += BatchSizeForHistory)
        {
            // Force garbage collection between batches to free memory
            if (batchIndex > 0)
            {
                GC.Collect(0, GCCollectionMode.Optimized);
            }
            
            var batchNumber = batchIndex / BatchSizeForHistory + 1;
            var totalBatches = Math.Ceiling(totalCount / (double)BatchSizeForHistory);
            
            logger.LogInformation("Processing batch {BatchNum}/{TotalBatches}", batchNumber, totalBatches);

            var fundingResultCount = await ProcessHistoricalBatch(
                exchangeId, 
                currentTimeMs,
                batchIndex,
                totalCount,
                exchangeInfoList,
                cancellationToken);
            
            // Delay between batches to respect API rate limits
            if (batchIndex + BatchSizeForHistory < totalCount)
            {
                await LaunchDelayForApi(fundingResultCount, cancellationToken);
            }
        }
        
        logger.LogInformation("Completed {Service} at {Now}. Symbols processed: {Total}",
            nameof(BaseFundingRateHistorySyncJob<ILogger>),
            DateTime.UtcNow,
            totalCount);
    }
    
    /// <summary>
    /// Process a single batch of historical data.
    /// </summary>
    private async Task<int> ProcessHistoricalBatch(
        Guid exchangeId,
        long currentTimeMs,
        int batchIndex,
        int totalCount,
        List<(object? ExchangeSymbol, object? FundingSymbol)> exchangeInfoList,
        CancellationToken cancellationToken = default)
    {
        var fundingResult = new ConcurrentBag<FundingRateHistory>();
        
        // Calculate batch boundaries
        var batchEndIndex = Math.Min(batchIndex + BatchSizeForHistory, totalCount);
        var currentBatch = exchangeInfoList.GetRange(batchIndex, batchEndIndex - batchIndex);
            
        // Process all symbols in the batch in parallel
        var batchTasks = new List<Task>(batchEndIndex - batchIndex);

        foreach (var symbolPair in currentBatch)
        {
            batchTasks.Add(ProcessHistorySymbolAsync(
                exchangeId, 
                currentTimeMs, 
                fundingResult, 
                Semaphore, 
                symbolPair,
                cancellationToken));
        }
            
        await Task.WhenAll(batchTasks);
            
        // Save batch results if any
        var fundingResultCount = fundingResult.Count;
        if (fundingResultCount != 0)
        {
            logger.LogInformation("Saving current batch: {Count} records", fundingResultCount);
            await SaveSymbolsAsync(fundingResult, cancellationToken);
        }

        return fundingResultCount;
    }

    /// <summary>
    /// Process historical data for a single symbol
    /// </summary>
    private async Task ProcessHistorySymbolAsync(
        Guid exchangeId,
        long currentTimeMs,
        ConcurrentBag<FundingRateHistory> fundingResult,
        SemaphoreSlim? semaphore,
        (object? ExchangeSymbol, object? FundingSymbol) symbolPair,
        CancellationToken cancellationToken = default)
    {
        if (semaphore != null)
        {
            await semaphore.WaitAsync(cancellationToken);
        }

        try
        {
            if (symbolPair.FundingSymbol == null)
            {
                logger.LogWarning("Funding symbol is null for symbol pair");
                return;
            }

            var fundingSymbolInfo = GetFundingSymbolInfo(symbolPair.FundingSymbol);

            // Determine the listing date for the symbol
            var listingDate = fundingSymbolInfo.LaunchTime;

            if (!listingDate.HasValue)
            {
                if (symbolPair.ExchangeSymbol != null)
                {
                    var exchangeSymbolInfo = GetExchangeSymbolInfo(symbolPair.ExchangeSymbol);
                    listingDate = exchangeSymbolInfo.ListingDate;
                }
            }
                    
            // Fetch all historical funding rates for the symbol
            var historicalFutures = await FetchSymbolHistoricalData(
                fundingSymbolInfo.SymbolName, 
                listingDate, 
                cancellationToken);

            var historicalFuturesCount = 0;
                    
            foreach (var future in historicalFutures)
            {
                historicalFuturesCount++;
                
                var fundingRate = CreateFundingRateHistory(
                    currentTimeMs,
                    exchangeId,
                    future,
                    fundingSymbolInfo);
                        
                fundingResult.Add(fundingRate);
            }
                    
            logger.LogInformation("Processed symbol: {SymbolName}, records retrieved: {Count}", 
                fundingSymbolInfo.SymbolName, historicalFuturesCount);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error processing symbol");
        }
        finally
        {
            semaphore?.Release();
        }
    }
    
    /// <summary>
    /// Fetch historical data for a symbol with retry logic.
    /// </summary>
    private async Task<IEnumerable<(decimal FundingRate, DateTime FundingTime, int? FundingIntervalHours)>> FetchSymbolHistoricalData(
        string symbolName,
        DateTime? listingDate,
        CancellationToken cancellationToken)
    {
        return await ExecuteWithRetry(
            () => FetchAllFundingRatesAsync(
                symbolName, 
                listingDate, 
                cancellationToken),
            cancellationToken);
    }
    
    /// <summary>
    /// Create and add new funding rate item
    /// </summary>
    private static void CreateFundingRate(
        long currentTimeMs,
        Guid exchangeId,
        ConcurrentQueue<FundingRateHistory> fundingResult,
        (decimal FundingRate, DateTime FundingTime, int? FundingIntervalHours) newFundingRate,
        (string SymbolName, int? FundingIntervalHours, DateTime? LaunchTime) fundingSymbolInfo)
    {
        var fundingRate = CreateFundingRateHistory(
            currentTimeMs,
            exchangeId,
            newFundingRate,
            fundingSymbolInfo);

        fundingResult.Enqueue(fundingRate);
    }
    
    /// <summary>
    /// Create funding rate history entity
    /// </summary>
    private static FundingRateHistory CreateFundingRateHistory(
        long currentTimeMs,
        Guid exchangeId,
        (decimal FundingRate, DateTime FundingTime, int? FundingIntervalHours) fundingData,
        (string SymbolName, int? FundingIntervalHours, DateTime? LaunchTime) fundingSymbolInfo)
    {
        // Validate funding rate data
        if (fundingData.FundingTime == default)
        {
            throw new InvalidOperationException($"Invalid funding time for symbol {fundingSymbolInfo.SymbolName}");
        }
    
        var fundingTimeUnix = ((DateTimeOffset)fundingData.FundingTime).ToUnixTimeMilliseconds();
    
        // Validate and get interval hours
        var intervalHours = fundingSymbolInfo.FundingIntervalHours ?? 
                            fundingData.FundingIntervalHours ?? 
                            throw new InvalidOperationException($"No funding interval found for {fundingSymbolInfo.SymbolName}");

        return new FundingRateHistory
        {
            Id = Guid.NewGuid(),
            ExchangeId = exchangeId,
            Symbol = NormalizeSymbol(fundingSymbolInfo.SymbolName),
            Name = fundingSymbolInfo.SymbolName,
            IntervalHours = intervalHours,
            Rate = fundingData.FundingRate,
            TsRate = fundingTimeUnix,
            FetchedAt = currentTimeMs
        };
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
        IEnumerable<FundingRateHistory> createdFundingResult, 
        CancellationToken cancellationToken = default)
    {
        // Skip if nothing to save
        if (!createdFundingResult.Any())
        {
            return;
        }
        
        try
        {
            // Create new rates with bulk
            await fundingRateHistoryRepository.BulkInsertAsync(createdFundingResult, cancellationToken);

            // unitOfWork.SaveAsync doesn't need fot bulk operation
        }
        catch (Exception ex)
        {
            throw new DatabaseException(
                "SymbolSyncJob", $"Error updating symbols for {ExchangeCode}", ex);
        }
    }
    
    /// <summary>
    /// Execute an operation with retry logic for handling transient failures.
    /// </summary>
    private async Task<T> ExecuteWithRetry<T>(
        Func<Task<T>> operation,
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
                logger.LogWarning(ex, "Operation failed, attempt {Attempt}/{MaxAttempts}. Retrying...", 
                    attempts, maxRetryAttempts);
                
                await Task.Delay(retryDelay * attempts, cancellationToken);
            }
        }
        
        // Final attempt without catching
        return await operation();
    }
}