using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace AgentWpf.Domain;

/// <summary>
/// Decides which processes a session may automate. A process is allowed when it was
/// launched or attached explicitly (trusted by process id) or when its executable name
/// is on the configured allowlist. An empty policy denies everything.
/// </summary>
public sealed class AllowlistPolicy
{
    // Upper bound for allowlist sizes; anything above indicates a configuration error.
    private const int MaxEntries = 1024;

    private readonly HashSet<string> processNames = new(StringComparer.OrdinalIgnoreCase);

    private readonly HashSet<int> trustedProcessIds = [];

    /// <summary>
    /// Adds an executable name (with or without <c>.exe</c>) to the allowlist.
    /// </summary>
    /// <param name="processName">The executable name, e.g. <c>notepad</c> or <c>notepad.exe</c>.</param>
    public void AllowProcessName(string processName)
    {
        Debug.Assert(!string.IsNullOrWhiteSpace(processName), "Precondition: processName must not be empty.");
        Debug.Assert(this.processNames.Count < MaxEntries, "Precondition: allowlist size exceeds the sane maximum.");

        var added = this.processNames.Add(Normalize(processName));

        Debug.Assert(added || this.processNames.Contains(Normalize(processName)), "Postcondition: name must be on the list.");
    }

    /// <summary>
    /// Trusts a process by id, typically because the session launched or explicitly attached it.
    /// </summary>
    /// <param name="processId">The id of the process to trust.</param>
    public void TrustProcess(int processId)
    {
        Debug.Assert(processId > 0, "Precondition: processId must be positive.");
        Debug.Assert(this.trustedProcessIds.Count < MaxEntries, "Precondition: trusted set exceeds the sane maximum.");

        this.trustedProcessIds.Add(processId);

        Debug.Assert(this.trustedProcessIds.Contains(processId), "Postcondition: process must be trusted.");
    }

    /// <summary>
    /// Revokes the trust of a process id, e.g. after it was closed or detached.
    /// </summary>
    /// <param name="processId">The id of the process to forget.</param>
    public void ForgetProcess(int processId)
    {
        Debug.Assert(processId > 0, "Precondition: processId must be positive.");

        this.trustedProcessIds.Remove(processId);

        Debug.Assert(!this.trustedProcessIds.Contains(processId), "Postcondition: process must no longer be trusted.");
    }

    /// <summary>
    /// Determines whether the given process may be automated.
    /// </summary>
    /// <param name="processName">The executable name of the process.</param>
    /// <param name="processId">The id of the process.</param>
    /// <returns><see langword="true"/> when the process is trusted or its name is allowlisted.</returns>
    public bool IsAllowed(string processName, int processId)
    {
        Debug.Assert(processName != null, "Precondition: processName must not be null.");
        Debug.Assert(processId > 0, "Precondition: processId must be positive.");

        return this.trustedProcessIds.Contains(processId)
            || this.processNames.Contains(Normalize(processName));
    }

    private static string Normalize(string processName)
    {
        var trimmed = processName.Trim();
        return trimmed.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) ? trimmed[..^4] : trimmed;
    }
}
