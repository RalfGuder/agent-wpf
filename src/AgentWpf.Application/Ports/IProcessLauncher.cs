using System;
using System.Collections.Generic;

namespace AgentWpf.Application.Ports;

/// <summary>
/// Starts, finds and terminates processes.
/// </summary>
public interface IProcessLauncher
{
    /// <summary>
    /// Starts an executable.
    /// </summary>
    /// <param name="path">The absolute path or a name resolvable via PATH.</param>
    /// <param name="arguments">The command line arguments.</param>
    /// <param name="workingDirectory">The working directory of the new process.</param>
    /// <returns>The id of the started process.</returns>
    int Start(string path, IReadOnlyList<string> arguments, string workingDirectory);

    /// <summary>
    /// Returns the ids of running processes with the given executable name.
    /// </summary>
    /// <param name="name">The executable name with or without <c>.exe</c>.</param>
    /// <returns>The matching process ids.</returns>
    IReadOnlyList<int> FindByName(string name);

    /// <summary>
    /// Waits for a process to exit.
    /// </summary>
    /// <param name="processId">The process id.</param>
    /// <param name="timeout">The maximum time to wait.</param>
    /// <returns><see langword="true"/> when the process has exited.</returns>
    bool WaitForExit(int processId, TimeSpan timeout);

    /// <summary>
    /// Terminates a process immediately.
    /// </summary>
    /// <param name="processId">The process id.</param>
    void Kill(int processId);
}
