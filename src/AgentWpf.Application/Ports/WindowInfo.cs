namespace AgentWpf.Application.Ports;

/// <summary>
/// A top-level window of the target process.
/// </summary>
/// <param name="RuntimeId">The textual UI Automation runtime id of the window.</param>
/// <param name="Title">The window title.</param>
/// <param name="IsModal">Whether the window is a modal dialog.</param>
/// <param name="IsForeground">Whether the window is the current foreground window.</param>
/// <param name="IsPopup">Whether the window is a transient popup such as an open menu or drop-down.</param>
public sealed record WindowInfo(string RuntimeId, string Title, bool IsModal, bool IsForeground, bool IsPopup = false);
