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

    /// <summary>
    /// Fetches active perpetual symbols from Binance that have funding rates.
    /// Combines funding info and exchange info to get complete symbol data.
    /// </summary>	
    protected abstract Task<IEnumerable<SymbolPairDto>> FetchSymbolsAsync(CancellationToken cancellationToken = default);
    /// <summary>
    /// Fetches all historical funding rates for a symbol starting from a specific time.
    /// Implements pagination to handle large datasets efficiently.
    /// </summary>
    protected abstract Task<IEnumerable<FuturesDataDto>> FetchAllFundingRatesAsync(string symbolName, DateTime? startTime, CancellationToken cancellationToken = default);
    /// <summary>
    /// Fetches the latest funding rate for a specific symbol.
    /// </summary>
    protected abstract Task<FuturesDataDto?> FetchFundingRateAsync(string symbolName, CancellationToken cancellationToken = default);
    /// <summary>
    /// Extracts exchange symbol information from object.
    /// </summary>
    protected abstract ExchangeInfoSymbol GetExchangeInfoSymbol(object symbol);
    /// <summary>
    /// Extracts funding  symbol information from object.
    /// </summary>
    protected abstract FundingInfoSymbol GetFundingInfoSymbol(object symbol);
    /// <summary>
    /// Implements dynamic delay between batches based on the number of API calls made.
    /// Helps to avoid hitting API rate limits during large historical data fetches.
    /// </summary>
    protected virtual Task LaunchDelayForApi(int count, CancellationToken cancellationToken) => Task.CompletedTask;

    /// <inheritdoc />
    public async Task<PaginationDto<List<FundingRateDto>>> GetExchangeLatestFundingRatesAsync(
        ExchangeCodeType exchangeCode,
        int pageNumber = 1,
        int pageSize = 10,
        CancellationToken cancellationToken = default)
    {
        logger.LogInformation("Getting latest exchange funding rates from history for {Exchange}. Page: {Page}, Size: {Size}", 
            exchangeCode, pageNumber, pageSize);

        // Validate exchange exists
        var exchange = await exchangeRepository.GetByCodeAsync(exchangeCode, cancellationToken);
        if (exchange == null)
        {
            logger.LogWarning("Exchange {ExchangeCode} not found", exchangeCode);
            throw new NotFoundException($"Exchange {exchangeCode} not found");
        }

        // Get latest funding rates for this exchange (latest record per symbol)
        var latestRates = await fundingRateHistoryRepository
            .GetLatestSymbolRatesAsync(
                f => f.ExchangeId == exchange.Id,
                true,
                pageNumber,
                pageSize,
                cancellationToken);

        // Get total count of unique symbols for this exchange
        var totalSymbolsCount = await fundingRateHistoryRepository
            .GetUniqueSymbolsCountAsync(f => f.ExchangeId == exchange.Id, cancellationToken);

        // Convert to DTOs
        var data = latestRates
            .Select(rate => new FundingRateDto(
                rate.Symbol,
                rate.Rate,
                rate.TsRate,
                rate.FetchedAt))
            .ToList();

        var totalPages = (int)Math.Ceiling(totalSymbolsCount / (double)pageSize);

        logger.LogInformation("Successfully retrieved {Count} latest funding rates for {Exchange}", 
            data.Count, exchangeCode);

        return new PaginationDto<List<FundingRateDto>>(
            data,
            pageNumber,
            pageSize,
            totalSymbolsCount,
            totalPages);
    }
    
    /// <inheritdoc/>
    public async Task<PaginationDto<List<FundingRateDto>>> GetSymbolExchangeHistoryAsync(
        string symbol,
        ExchangeCodeType exchangeCode,
        int pageNumber = 1,
        int pageSize = 10,
        CancellationToken cancellationToken = default)
    {
        logger.LogInformation("Getting symbol exchange history for {Symbol} on {Exchange}. Page: {Page}, Size: {Size}", 
            symbol, exchangeCode, pageNumber, pageSize);

        // Validate exchange exists
        var exchange = await exchangeRepository.GetByCodeAsync(exchangeCode, cancellationToken);
        if (exchange == null)
        {
            logger.LogWarning("Exchange {ExchangeCode} not found", exchangeCode);
            throw new NotFoundException($"Exchange {exchangeCode} not found");
        }

        // Get historical data for specific symbol on specific exchange
        var historyData = await fundingRateHistoryRepository
            .GetByFilterAsync(
                f => f.Symbol == symbol && f.ExchangeId == exchange.Id,
                pageNumber,
                pageSize,
                cancellationToken);

        // Get total count for pagination
        var totalCount = await fundingRateHistoryRepository
            .GetCountByFilterAsync(
                f => f.Symbol == symbol && f.ExchangeId == exchange.Id, 
                cancellationToken);

        // Convert to DTOs
        var data = historyData
            .Select(rate => new FundingRateDto(
                rate.Symbol,
                rate.Rate,
                rate.TsRate,
                rate.FetchedAt))
            .ToList();

        var totalPages = (int)Math.Ceiling(totalCount / (double)pageSize);

        logger.LogInformation("Successfully retrieved {Count} history records for {Symbol} on {Exchange}", 
            data.Count, symbol, exchangeCode);

        return new PaginationDto<List<FundingRateDto>>(
            data,
            pageNumber,
            pageSize,
            totalCount,
            totalPages);
    }
    
    /// <inheritdoc />
    public async Task<PaginationDto<List<SymbolFundingRatesDto>>> GetLatestSymbolFundingRatesAsync(
        int pageNumber = 1,
        int pageSize = 10,
        CancellationToken cancellationToken = default)
    {
        logger.LogInformation("Getting latest symbol funding rates from history. Page: {Page}, Size: {Size}", 
            pageNumber, pageSize);

        var fundingEntities = await fundingRateHistoryRepository
            .GetLatestSymbolRatesAsync(null, false, pageNumber, pageSize, cancellationToken);

        var totalSymbols = await fundingRateHistoryRepository
            .GetUniqueSymbolsCountAsync(cancellationToken: cancellationToken);

        // Group by symbol
        var groupedBySymbol = fundingEntities
            .GroupBy(f => f.Symbol)
            .ToDictionary(g => g.Key, g => g.ToList());

        // Convert to DTOs
        var symbolFundingData = ConvertToSymbolFundingRatesDto(groupedBySymbol);

        var totalPages = (int)Math.Ceiling(totalSymbols / (double)pageSize);

        logger.LogInformation("Successfully retrieved {Count} symbols from history", symbolFundingData.Count);

        return new PaginationDto<List<SymbolFundingRatesDto>>(
            symbolFundingData,
            pageNumber,
            pageSize,
            totalSymbols,
            totalPages);
    }

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

    /// <summary>
    /// Converts grouped domain entities to DTOs
    /// </summary>
    private static List<SymbolFundingRatesDto> ConvertToSymbolFundingRatesDto(
        Dictionary<string, List<FundingRateHistory>> groupedEntities)
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
    /// Process latest historical data and save to database.
    /// </summary>
    private async Task ProcessLatestHistoricalData(
        IEnumerable<SymbolPairDto> symbols,
        IEnumerable<FundingRateHistory> allExistingFundingRates,
        ConcurrentQueue<FundingRateDto> fundingResultDtos,
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
        
        foreach (var symbolObject in symbolsList)
        {
            tasks.Add(ProcessLatestSymbolAsync(
                currentTimeMs, 
                exchangeId, 
                Semaphore,
                symbolObject,
                existingRatesDict,
                fundingResult,
                fundingResultDtos, 
                cancellationToken));
        }

        await Task.WhenAll(tasks);
        
        logger.LogInformation("Completed {Service} at {Now}. Symbols processed: {Total}. Symbols updated: {Updated}",
            nameof(BaseFundingRateHistoryService<ILogger>),
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
        SymbolPairDto symbolObject,
        Dictionary<string, FundingRateHistory> existingRatesDict,
        ConcurrentQueue<FundingRateHistory> fundingResult,
        ConcurrentQueue<FundingRateDto> fundingResultDtos,
        CancellationToken cancellationToken = default)
    {
        await semaphore.WaitAsync(cancellationToken);

        try
        {
            var fundingInfoSymbol = GetFundingInfoSymbol(symbolObject.FundingSymbols!);

            // Check if symbol needs update
            if (existingRatesDict.TryGetValue(fundingInfoSymbol.SymbolName, out var existFundingRate))
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
                        fundingInfoSymbol,
                        fundingResult,
                        fundingResultDtos,
                        cancellationToken);
                }
                // Process the latest funding rate
                else
                {
                    await ProcessLatestFundingRate(
                        currentTimeMs,
                        exchangeId,
                        fundingResult,
                        fundingResultDtos,
                        fundingInfoSymbol,
                        cancellationToken);
                }
            }
            
            // Process all historical data for a non-existent symbol
            var historicalFundings = new ConcurrentBag<FundingRateHistory>();
            
            await ProcessHistorySymbolAsync(
                exchangeId,
                currentTimeMs,
                historicalFundings,
                null,
                symbolObject,
                cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Error processing symbol {Symbol}", symbolObject);
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
        FundingInfoSymbol fundingInfoSymbol,
        ConcurrentQueue<FundingRateHistory> fundingResult,
        ConcurrentQueue<FundingRateDto> fundingResultDtos,
        CancellationToken cancellationToken)
    {
        // Calculate the start time for fetching missed rates
        var lastFundingRate = DateTimeOffset.FromUnixTimeMilliseconds(existingRate.TsRate).DateTime;
        lastFundingRate = lastFundingRate.AddMilliseconds(1);

        // Fetch all missed funding rates with retry logic
        var fundingRateGaps = await ExecuteWithRetry(
            () => FetchAllFundingRatesAsync(
                fundingInfoSymbol.SymbolName,
                lastFundingRate,
                cancellationToken),
            cancellationToken);

        // Process each missed funding rate
        foreach (var newFundingRateGap in fundingRateGaps)
        {
            CreateFundingRate(
                fundingResultDtos,
                currentTimeMs,
                exchangeId,
                fundingResult,
                newFundingRateGap,
                fundingInfoSymbol);
        }
    }
    
    /// <summary>
    /// Process the latest funding rate for a symbol.
    /// </summary>
    private async Task ProcessLatestFundingRate(
        long currentTimeMs,
        Guid exchangeId,
        ConcurrentQueue<FundingRateHistory> fundingResult,
        ConcurrentQueue<FundingRateDto> fundingResultDtos,
        FundingInfoSymbol fundingInfoSymbol,
        CancellationToken cancellationToken = default)
    {
        // Fetch the latest funding rate with retry logic
        var newFundingRate = await ExecuteWithRetry(
            () => FetchFundingRateAsync(fundingInfoSymbol.SymbolName, cancellationToken),
            cancellationToken);
        
        if (newFundingRate != null)
        {
            CreateFundingRate(
                fundingResultDtos,
                currentTimeMs,
                exchangeId,
                fundingResult,
                newFundingRate,
                fundingInfoSymbol);
        }
        else
        {
            logger.LogWarning("Failed to fetch latest funding rate for symbol {Symbol}", fundingInfoSymbol.SymbolName);
        }
    }

    /// <summary>
    /// Process all historical data for all time and save to database.
    /// </summary>
    private async Task ProcessAllHistoricalData(
        IEnumerable<SymbolPairDto> exchangeInfo,
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
            nameof(BaseFundingRateHistoryService<ILogger>),
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
        List<SymbolPairDto> exchangeInfoList,
        CancellationToken cancellationToken = default)
    {
        var fundingResult = new ConcurrentBag<FundingRateHistory>();
        
        // Calculate batch boundaries
        var batchEndIndex = Math.Min(batchIndex + BatchSizeForHistory, totalCount);
        var currentBatch = exchangeInfoList.GetRange(batchIndex, batchEndIndex - batchIndex);
            
        // Process all symbols in the batch in parallel
        var batchTasks = new List<Task>(batchEndIndex - batchIndex);

        foreach (var symbolObject in currentBatch)
        {
            batchTasks.Add(ProcessHistorySymbolAsync(
                exchangeId, 
                currentTimeMs, 
                fundingResult, 
                Semaphore, 
                symbolObject,
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
        SymbolPairDto symbolObject,
        CancellationToken cancellationToken = default)
    {
        if (semaphore != null)
        {
            await semaphore.WaitAsync(cancellationToken);
        }

        try
        {
            var fundingInfoSymbol = GetFundingInfoSymbol(symbolObject.FundingSymbols!);

            // Determine the listing date for the symbol
            var listingDate = fundingInfoSymbol.LaunchTime;

            if (!listingDate.HasValue)
            {
                if (symbolObject.ExchangeSymbol != null)
                {
                    // For binance the fundingIntervalHours exist in this object
                    var exchangeInfoSymbol = GetExchangeInfoSymbol(symbolObject.ExchangeSymbol);

                    listingDate = exchangeInfoSymbol.ListingDate;
                }
            }
                    
            // Fetch all historical funding rates for the symbol
            var historicalFutures = await FetchSymbolHistoricalData(
                fundingInfoSymbol.SymbolName, 
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
                    fundingInfoSymbol);
                        
                fundingResult.Add(fundingRate);
            }
                    
            logger.LogInformation("Processed symbol: {SymbolName}, records retrieved: {Count}", 
                fundingInfoSymbol.SymbolName, historicalFuturesCount);
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
    private async Task<IEnumerable<FuturesDataDto>> FetchSymbolHistoricalData(
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
        ConcurrentQueue<FundingRateDto> fundingResultDtos,
        long currentTimeMs,
        Guid exchangeId,
        ConcurrentQueue<FundingRateHistory> fundingResult,
        FuturesDataDto newFundingRate,
        FundingInfoSymbol fundingInfoSymbol)
    {
        var fundingRate = CreateFundingRateHistory(
            currentTimeMs,
            exchangeId,
            newFundingRate,
            fundingInfoSymbol);

        var fundingRateDto = new FundingRateDto(
            fundingInfoSymbol.SymbolName,
            newFundingRate.FundingRate,
            fundingRate.TsRate,
            currentTimeMs);

        fundingResult.Enqueue(fundingRate);
        fundingResultDtos.Enqueue(fundingRateDto);
    }
    
    /// <summary>
    /// Create funding rate history entity
    /// </summary>
    private static FundingRateHistory CreateFundingRateHistory(
        long currentTimeMs,
        Guid exchangeId,
        FuturesDataDto fundingData,
        FundingInfoSymbol fundingInfoSymbol)
    {
        // Validate funding rate data
        if (fundingData.FundingTime == default)
        {
            throw new InvalidOperationException($"Invalid funding time for symbol {fundingInfoSymbol.SymbolName}");
        }
    
        var fundingTimeUnix = ((DateTimeOffset)fundingData.FundingTime).ToUnixTimeMilliseconds();
    
        // Validate and get interval hours
        var intervalHours = fundingInfoSymbol.FundingIntervalHours ?? 
                            fundingData.FundingIntervalHours ?? 
                            throw new InvalidOperationException($"No funding interval found for {fundingInfoSymbol.SymbolName}");

        return new FundingRateHistory
        {
            Id = Guid.NewGuid(),
            ExchangeId = exchangeId,
            Symbol = NormalizeSymbol(fundingInfoSymbol.SymbolName),
            Name = fundingInfoSymbol.SymbolName,
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