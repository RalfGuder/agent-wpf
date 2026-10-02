namespace AgentWpf.Infrastructure.FlaUI;

/// <summary>
/// Process-wide Win32 settings that must be applied before any UI Automation call.
/// </summary>
public static class NativeMethodsBootstrap
{
    /// <summary>
    /// Makes the process per-monitor DPI aware (v2) so that UIA bounds and captured pixels share
    /// one physical coordinate space. Has no effect when the awareness is already set.
    /// </summary>
    /// <returns><see langword="true"/> when the awareness was set by this call.</returns>
    public static bool EnableDpiAwareness() => NativeMethods.SetProcessDpiAwarenessContext(NativeMethods.DpiAwarenessContextPerMonitorAwareV2);
}
