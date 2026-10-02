using System.Diagnostics;

namespace AgentWpf.Domain;

/// <summary>
/// Error categories reported by agent-wpf. The numeric value is the process exit code.
/// </summary>
public enum ErrorCode
{
    /// <summary>The command succeeded.</summary>
    None = 0,

    /// <summary>The action could not be performed, e.g. the element lacks the required pattern.</summary>
    ActionFailed = 1,

    /// <summary>The command line was invalid.</summary>
    Usage = 2,

    /// <summary>The referenced element no longer exists; a new snapshot is required.</summary>
    StaleRef = 3,

    /// <summary>A wait condition was not met within the timeout.</summary>
    Timeout = 4,

    /// <summary>The target process is not on the session allowlist.</summary>
    NotAllowed = 5,

    /// <summary>Access was denied, typically because the target runs elevated (UIPI).</summary>
    AccessDenied = 6,
}

/// <summary>
/// Extension methods for <see cref="ErrorCode"/>.
/// </summary>
public static class ErrorCodeExtensions
{
    /// <summary>
    /// Returns the stable, machine-readable kebab-case name used in JSON output.
    /// </summary>
    /// <param name="code">The error code.</param>
    /// <returns>The kebab-case name, e.g. <c>stale-ref</c>.</returns>
    public static string ToKebabName(this ErrorCode code)
    {
        Debug.Assert(System.Enum.IsDefined(code), "Precondition: code must be a defined ErrorCode.");

        var name = code switch
        {
            ErrorCode.None => "none",
            ErrorCode.ActionFailed => "action-failed",
            ErrorCode.Usage => "usage",
            ErrorCode.StaleRef => "stale-ref",
            ErrorCode.Timeout => "timeout",
            ErrorCode.NotAllowed => "not-allowed",
            ErrorCode.AccessDenied => "access-denied",
            _ => "unknown",
        };

        Debug.Assert(name.Length > 0, "Postcondition: name must not be empty.");
        return name;
    }
}
