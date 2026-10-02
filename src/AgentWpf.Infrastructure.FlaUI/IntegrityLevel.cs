using System;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace AgentWpf.Infrastructure.FlaUI;

/// <summary>
/// Reads Windows integrity levels to detect targets that UIPI shields from this process.
/// </summary>
public static class IntegrityLevel
{
    /// <summary>
    /// Determines whether a process runs with a higher integrity level than the current process.
    /// </summary>
    /// <param name="processId">The process id.</param>
    /// <returns><see langword="true"/> when the target is more privileged or cannot even be inspected.</returns>
    public static bool IsHigherThanCurrent(int processId)
    {
        Debug.Assert(processId > 0, "Precondition: processId must be positive.");

        var own = Of(Environment.ProcessId);
        var target = Of(processId);

        Debug.Assert(own.HasValue, "Invariant: the own integrity level is always readable.");
        return target is null || target > own;
    }

    // Returns the mandatory integrity RID (e.g. 0x2000 medium, 0x3000 high), or null when unreadable.
    private static int? Of(int processId)
    {
        var process = NativeMethods.OpenProcess(NativeMethods.ProcessQueryLimitedInformation, false, processId);
        if (process == 0)
        {
            return null;
        }

        try
        {
            if (!NativeMethods.OpenProcessToken(process, NativeMethods.TokenQuery, out var token))
            {
                return null;
            }

            try
            {
                return ReadIntegrity(token);
            }
            finally
            {
                NativeMethods.CloseHandle(token);
            }
        }
        finally
        {
            NativeMethods.CloseHandle(process);
        }
    }

    private static int? ReadIntegrity(nint token)
    {
        NativeMethods.GetTokenInformation(token, NativeMethods.TokenIntegrityLevel, 0, 0, out var length);
        if (length <= 0)
        {
            return null;
        }

        var buffer = Marshal.AllocHGlobal(length);
        try
        {
            if (!NativeMethods.GetTokenInformation(token, NativeMethods.TokenIntegrityLevel, buffer, length, out _))
            {
                return null;
            }

            // TOKEN_MANDATORY_LABEL starts with SID_AND_ATTRIBUTES, whose first field is the SID pointer.
            var sid = Marshal.ReadIntPtr(buffer);
            var count = Marshal.ReadByte(NativeMethods.GetSidSubAuthorityCount(sid));
            return Marshal.ReadInt32(NativeMethods.GetSidSubAuthority(sid, (uint)(count - 1)));
        }
        finally
        {
            Marshal.FreeHGlobal(buffer);
        }
    }
}
