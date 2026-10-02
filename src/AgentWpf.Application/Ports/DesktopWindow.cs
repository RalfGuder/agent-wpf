namespace AgentWpf.Application.Ports;

/// <summary>
/// A visible top-level window on the desktop, used to resolve <c>attach --title</c>.
/// </summary>
/// <param name="ProcessId">The id of the owning process.</param>
/// <param name="Title">The window title.</param>
public sealed record DesktopWindow(int ProcessId, string Title);
