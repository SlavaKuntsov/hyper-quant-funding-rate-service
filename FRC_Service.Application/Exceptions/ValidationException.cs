namespace FRC_Service.Application.Exceptions;

/// <summary>
/// Exception thrown when a validation error occurs
/// </summary>
public class ValidationException : Exception
{
    /// <summary>
    /// Gets the validation errors
    /// </summary>
    public IDictionary<string, string[]> Errors { get; }

    /// <summary>
    /// Initializes a new instance of the ValidationException class
    /// </summary>
    public ValidationException()
        : base("One or more validation failures have occurred.")
    {
        Errors = new Dictionary<string, string[]>();
    }
    
    /// <summary>
    /// Initializes a new instance of the ValidationException class
    /// </summary>
    public ValidationException(string key, string message)
        : base("One or more validation failures have occurred.")
    {
        Errors = new Dictionary<string, string[]>() { { key, [message] } };
    }

    /// <summary>
    /// Initializes a new instance of the ValidationException class
    /// </summary>
    /// <param name="errors">Dictionary of property names and their validation error messages</param>
    public ValidationException(IDictionary<string, string[]> errors)
        : this()
    {
        Errors = errors ?? new Dictionary<string, string[]>();
    }

    /// <summary>
    /// Initializes a new instance of the ValidationException class
    /// </summary>
    /// <param name="errors">Dictionary of property names and their validation error messages</param>
    public ValidationException(IDictionary<string, string> errors)
        : this()
    {
        if (errors == null)
        {
            Errors = new Dictionary<string, string[]>();
            return;
        }

        Errors = errors.ToDictionary(
            kvp => kvp.Key,
            kvp => new[] { kvp.Value }
        );
    }
}
