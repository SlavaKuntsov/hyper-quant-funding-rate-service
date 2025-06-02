using FRC_Service.Domain.Enums;

namespace FRC_Service.Domain.Models;

/// <summary>
/// Represents an exchange (trading platform).
/// </summary>
public class Exchange
{
    /// <summary>
    /// Unique identifier.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Type of the exchange.
    /// </summary>
    public ExchangeCodeType Code { get; set; }
}