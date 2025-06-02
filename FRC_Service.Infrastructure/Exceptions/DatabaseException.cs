namespace FRC_Service.Infrastructure.Exceptions
{
    public class DatabaseException : Exception
    {
        public string ContextName { get; }

        public DatabaseException(
            string contextName, string message, Exception? innerException = null)
            : base(message, innerException)
        {
            ContextName = contextName;
        }
    }
}