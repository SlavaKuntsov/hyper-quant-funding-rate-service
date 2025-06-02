using System.Reflection;

namespace FRC_Service.Infrastructure;

/// <summary>
/// Provides a static reference to the assembly containing this class.
/// </summary>
public static class AssemblyReference
{
    /// <summary>
    /// Gets the assembly that contains the <see cref="AssemblyReference"/> class.
    /// </summary>
    public static Assembly Assembly => typeof(AssemblyReference).Assembly;
}