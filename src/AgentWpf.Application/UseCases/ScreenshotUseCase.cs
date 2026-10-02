using System.Globalization;
using System.IO;
using AgentWpf.Application.Ports;
using AgentWpf.Application.Sessions;
using AgentWpf.Domain;

namespace AgentWpf.Application.UseCases;

/// <summary>
/// The <c>screenshot</c> command: captures the active window or one element as PNG.
/// </summary>
public sealed class ScreenshotUseCase : SessionUseCase
{
    private readonly IScreenCapture capture;

    /// <summary>
    /// Initializes a new instance of the <see cref="ScreenshotUseCase"/> class.
    /// </summary>
    /// <param name="session">The session.</param>
    /// <param name="driver">The automation driver.</param>
    /// <param name="capture">The screen capture.</param>
    public ScreenshotUseCase(AgentSession session, IAutomationDriver driver, IScreenCapture capture)
        : base(session, driver)
    {
        this.capture = capture;
    }

    /// <summary>
    /// Saves a screenshot.
    /// </summary>
    /// <param name="path">The absolute target path.</param>
    /// <param name="element">The element to crop to, or <see langword="null"/> for the whole window.</param>
    /// <returns>The result naming the file and size.</returns>
    public CommandResult Execute(string path, RefId? element)
    {
        System.Diagnostics.Debug.Assert(!string.IsNullOrWhiteSpace(path), "Precondition: path must not be empty.");
        System.Diagnostics.Debug.Assert(Path.IsPathRooted(path), "Precondition: path must be absolute.");

        var window = this.ResolveWindow(this.RequireWindows());
        var crop = element is RefId r ? this.OnElement(r, id => this.Driver.Capture(id, 0).Bounds) : null;
        if (element is RefId e && (crop is null || crop.Value.IsEmpty))
        {
            throw new AgentWpfException(ErrorCode.ActionFailed, string.Format(CultureInfo.InvariantCulture, "@{0} has no visible area.", e), "scroll it into view with scroll @ref");
        }

        var (width, height) = this.capture.SavePng(this.Driver.GetWindowHandle(window.RuntimeId), crop, path);

        System.Diagnostics.Debug.Assert(width > 0 && height > 0, "Postcondition: the image must not be empty.");
        return new CommandResult(string.Format(CultureInfo.InvariantCulture, "saved {0} ({1}x{2})\n", path, width, height));
    }
}
