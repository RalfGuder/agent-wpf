using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using AgentWpf.Application.Ports;
using AgentWpf.Domain;

namespace AgentWpf.Infrastructure.FlaUI;

/// <summary>
/// Starts and terminates processes with <see cref="Process"/>.
/// </summary>
public sealed class ProcessLauncher : IProcessLauncher
{
    /// <inheritdoc />
    public int Start(string path, IReadOnlyList<string> arguments, string workingDirectory)
    {
        Debug.Assert(!string.IsNullOrWhiteSpace(path), "Precondition: path must not be empty.");
        Debug.Assert(arguments != null, "Precondition: arguments must not be null.");

        var start = new ProcessStartInfo(path) { UseShellExecute = false, WorkingDirectory = workingDirectory };
        foreach (var argument in arguments)
        {
            start.ArgumentList.Add(argument);
        }

        try
        {
            using var process = Process.Start(start)
                ?? throw new AgentWpfException(ErrorCode.ActionFailed, "Could not start " + path + ".", null);
            Debug.Assert(process.Id > 0, "Postcondition: a started process has an id.");
            return process.Id;
        }
        catch (Win32Exception ex)
        {
            throw new AgentWpfException(ErrorCode.ActionFailed, "Could not start " + path + ": " + ex.Message, "check the path; relative paths resolve against the current directory");
        }
    }

    /// <inheritdoc />
    public IReadOnlyList<int> FindByName(string name)
    {
        Debug.Assert(!string.IsNullOrWhiteSpace(name), "Precondition: name must not be empty.");

        var normalized = name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) ? name[..^4] : name;
        var processes = Process.GetProcessesByName(normalized);
        try
        {
            return processes.Select(p => p.Id).Order().ToList();
        }
        finally
        {
            foreach (var process in processes)
            {
                process.Dispose();
            }
        }
    }

    /// <inheritdoc />
    public bool WaitForExit(int processId, TimeSpan timeout)
    {
        Debug.Assert(processId > 0, "Precondition: processId must be positive.");
        Debug.Assert(timeout >= TimeSpan.Zero, "Precondition: timeout must not be negative.");

        try
        {
            using var process = Process.GetProcessById(processId);
            return process.WaitForExit(timeout);
        }
        catch (ArgumentException)
        {
            return true;
        }
    }

    /// <inheritdoc />
    public void Kill(int processId)
    {
        Debug.Assert(processId > 0, "Precondition: processId must be positive.");

        try
        {
            using var process = Process.GetProcessById(processId);
            process.Kill(entireProcessTree: true);
        }
        catch (ArgumentException)
        {
            // Already gone.
        }
    }
}
