using System;

namespace AgentWpf.Application.Ports;

/// <summary>
/// Thrown by an <see cref="IAutomationDriver"/> when an element with the given runtime id no longer exists.
/// </summary>
public sealed class ElementGoneException : Exception
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ElementGoneException"/> class.
    /// </summary>
    public ElementGoneException()
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ElementGoneException"/> class.
    /// </summary>
    /// <param name="message">The message.</param>
    public ElementGoneException(string message)
        : base(message)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ElementGoneException"/> class.
    /// </summary>
    /// <param name="message">The message.</param>
    /// <param name="innerException">The underlying exception.</param>
    public ElementGoneException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
