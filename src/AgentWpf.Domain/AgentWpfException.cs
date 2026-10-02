using System;
using System.Diagnostics;

namespace AgentWpf.Domain;

/// <summary>
/// An expected failure that is reported to the agent with an error code and an actionable hint.
/// </summary>
public sealed class AgentWpfException : Exception
{
    /// <summary>
    /// Initializes a new instance of the <see cref="AgentWpfException"/> class.
    /// </summary>
    public AgentWpfException()
        : this(ErrorCode.ActionFailed, "Action failed.", null)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="AgentWpfException"/> class.
    /// </summary>
    /// <param name="message">The English error message.</param>
    public AgentWpfException(string message)
        : this(ErrorCode.ActionFailed, message, null)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="AgentWpfException"/> class.
    /// </summary>
    /// <param name="message">The English error message.</param>
    /// <param name="innerException">The underlying exception.</param>
    public AgentWpfException(string message, Exception innerException)
        : base(message, innerException)
    {
        this.Code = ErrorCode.ActionFailed;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="AgentWpfException"/> class.
    /// </summary>
    /// <param name="code">The error category; must not be <see cref="ErrorCode.None"/>.</param>
    /// <param name="message">The English error message.</param>
    /// <param name="hint">An optional next step for the agent, e.g. <c>run snapshot again</c>.</param>
    public AgentWpfException(ErrorCode code, string message, string? hint)
        : base(message)
    {
        Debug.Assert(code != ErrorCode.None, "Precondition: an exception must carry a failure code.");
        Debug.Assert(!string.IsNullOrWhiteSpace(message), "Precondition: message must not be empty.");

        this.Code = code;
        this.Hint = hint;
    }

    /// <summary>
    /// Gets the error category, which is also the exit code.
    /// </summary>
    public ErrorCode Code { get; }

    /// <summary>
    /// Gets an optional next step for the agent.
    /// </summary>
    public string? Hint { get; }
}
