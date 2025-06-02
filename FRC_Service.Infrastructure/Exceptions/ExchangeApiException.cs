namespace FRC_Service.Infrastructure.Exceptions
{
    public class ExchangeApiException : Exception
    {
        public string ExchangeName { get; }
        public string MarketType { get; }

        public ExchangeApiException(
            string exchangeName, string marketType, string message, Exception? innerException = null)
            : base(message, innerException)
        {
            ExchangeName = exchangeName;
            MarketType = marketType;
        }
    }
}