using System.Diagnostics;

namespace AgentWpf.Application.UseCases;

/// <summary>
/// How <c>attach</c> identifies the process: by id, executable name or window title pattern.
/// </summary>
public sealed record AttachTarget
{
    private AttachTarget()
    {
    }

    /// <summary>Gets the process id, when attaching by <c>--pid</c>.</summary>
    public int? ProcessId { get; private init; }

    /// <summary>Gets the executable name, when attaching by <c>--process</c>.</summary>
    public string? ProcessName { get; private init; }

    /// <summary>Gets the title regular expression, when attaching by <c>--title</c>.</summary>
    public string? TitlePattern { get; private init; }

    /// <summary>Creates a target for <c>--pid</c>.</summary>
    /// <param name="processId">The process id.</param>
    /// <returns>The target.</returns>
    public static AttachTarget ForProcessId(int processId)
    {
        Debug.Assert(processId > 0, "Precondition: processId must be positive.");
        return new AttachTarget { ProcessId = processId };
    }

    /// <summary>Creates a target for <c>--process</c>.</summary>
    /// <param name="processName">The executable name.</param>
    /// <returns>The target.</returns>
    public static AttachTarget ForProcessName(string processName)
    {
        Debug.Assert(!string.IsNullOrWhiteSpace(processName), "Precondition: processName must not be empty.");
        return new AttachTarget { ProcessName = processName };
    }

    /// <summary>Creates a target for <c>--title</c>.</summary>
    /// <param name="titlePattern">The regular expression matched against window titles.</param>
    /// <returns>The target.</returns>
    public static AttachTarget ForTitle(string titlePattern)
    {
        Debug.Assert(!string.IsNullOrEmpty(titlePattern), "Precondition: titlePattern must not be empty.");
        return new AttachTarget { TitlePattern = titlePattern };
    }
}
