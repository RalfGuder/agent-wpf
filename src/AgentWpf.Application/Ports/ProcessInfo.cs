namespace AgentWpf.Application.Ports;

/// <summary>
/// Facts about a process that a session may target.
/// </summary>
/// <param name="ProcessId">The process id.</param>
/// <param name="Name">The executable name without extension.</param>
/// <param name="IsElevated">Whether the process runs with a higher integrity level than agent-wpf.</param>
public sealed record ProcessInfo(int ProcessId, string Name, bool IsElevated);
