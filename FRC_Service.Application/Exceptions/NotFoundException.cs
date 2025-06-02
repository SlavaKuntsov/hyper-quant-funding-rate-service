namespace FRC_Service.Application.Exceptions;

/// <summary>
/// Exception thrown when a requested resource is not found.
/// </summary>
public class NotFoundException : Exception
{
    /// <summary>
    /// Initializes a new instance of the NotFoundException class.
    /// </summary>
    public NotFoundException()
        : base("The requested resource was not found.")
    {
    }

    /// <summary>
    /// Initializes a new instance of the NotFoundException class, providing error message.
    /// </summary>
    /// <param name="message"></param>
    public NotFoundException(string message)
        : base(message)
    {
    }

    /// <summary>
    /// Initializes a new instance of the NotFoundException class, providing resource name and identifier.
    /// </summary>
    /// <param name="name"></param>
    /// <param name="key"></param>
    public NotFoundException(string name, object key)
        : base($"Entity \"{name}\" with identifier {key} was not found.")
    {
    }
}
