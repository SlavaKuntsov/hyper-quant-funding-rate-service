namespace FRC_Service.Domain.Models;

/// <summary>
/// Represents a historical data on Funding Rates for each trading pair on each exchange.
/// </summary>
public class FundingRateHistory
{
    /// <summary>
    /// Unique identifier for the symbol.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Identifier of the exchange this symbol belongs to.
    /// </summary>
    public Guid ExchangeId { get; set; }

    /// <summary>
    /// Trading symbol of the coin pair (e.g., 'BTCUSDT', 'ETHUSDT').
    /// </summary>
    public string Symbol { get; set; } = null!;

    /// <summary>
    /// Name of the coin.
    /// </summary>
    public string Name { get; set; } = null!;

    /// <summary>
    /// The time interval in hours for which the funding is updated.
    /// </summary>
    public int IntervalHours { get; set; }

    /// <summary>
    /// Funding Rate value.
    /// </summary>
    public decimal Rate { get; set; }

    /// <summary>
    /// Open interest (volume of open positions).
    /// </summary>
    public decimal OpenInterest { get; set; }
    
    /// <summary>
    /// Timestamp of funding.
    /// </summary>
    public long TsRate { get; set; }
    
    /// <summary>
    /// Timestamp 
    /// </summary>
    public long FetchedAt { get; set; } = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

    public Exchange? Exchange { get; set; }
}