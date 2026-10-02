using AgentWpf.Domain;

namespace AgentWpf.Application.Ports;

/// <summary>
/// Captures window content into PNG files.
/// </summary>
public interface IScreenCapture
{
    /// <summary>
    /// Captures a window, even when it is covered by other windows, and saves it as PNG.
    /// </summary>
    /// <param name="windowHandle">The native window handle.</param>
    /// <param name="crop">An optional screen rectangle to cut out, e.g. an element's bounds.</param>
    /// <param name="path">The absolute target path.</param>
    /// <returns>The size of the saved image.</returns>
    (int Width, int Height) SavePng(nint windowHandle, Bounds? crop, string path);
}
