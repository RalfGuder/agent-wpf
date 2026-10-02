using System.Globalization;
using System.Linq;
using System.Text;
using AgentWpf.Application.Ports;
using AgentWpf.Application.Sessions;
using AgentWpf.Application.Snapshots;
using AgentWpf.Domain;

namespace AgentWpf.Application.UseCases;

/// <summary>
/// Parameters of the <c>snapshot</c> command.
/// </summary>
public sealed record SnapshotRequest
{
    /// <summary>Gets the filter and rendering options.</summary>
    public SnapshotOptions Options { get; init; } = new();

    /// <summary>Gets the ref of the subtree to render (<c>-s @eN</c>), if any.</summary>
    public RefId? Scope { get; init; }

    /// <summary>Gets a value indicating whether all windows are rendered (<c>--all-windows</c>).</summary>
    public bool AllWindows { get; init; }
}

/// <summary>
/// Captures the automation tree of the active window (or a scope) and renders it with refs.
/// </summary>
public sealed class SnapshotUseCase : SessionUseCase
{
    /// <summary>
    /// Initializes a new instance of the <see cref="SnapshotUseCase"/> class.
    /// </summary>
    /// <param name="session">The session.</param>
    /// <param name="driver">The automation driver.</param>
    public SnapshotUseCase(AgentSession session, IAutomationDriver driver)
        : base(session, driver)
    {
    }

    /// <summary>
    /// Takes the snapshot.
    /// </summary>
    /// <param name="request">The snapshot parameters.</param>
    /// <returns>The rendered snapshot.</returns>
    public CommandResult Execute(SnapshotRequest request)
    {
        System.Diagnostics.Debug.Assert(request != null, "Precondition: request must not be null.");
        this.RequireTarget();

        var text = new StringBuilder();
        if (request.Scope is RefId scope)
        {
            text.Append(this.Render(this.OnElement(scope, id => this.Driver.Capture(id, SnapshotRenderer.MaxTreeDepth)), request.Options));
        }
        else
        {
            this.RenderWindows(request, text);
        }

        System.Diagnostics.Debug.Assert(text.Length > 0, "Postcondition: a snapshot is never empty.");
        return new CommandResult(text.ToString());
    }

    private void RenderWindows(SnapshotRequest request, StringBuilder text)
    {
        var windows = this.RequireWindows();
        if (request.AllWindows)
        {
            foreach (var window in windows)
            {
                text.Append(this.RenderWindow(window, request.Options));
            }

            return;
        }

        var active = this.ResolveWindow(windows);
        var regular = windows.Count(w => !w.IsPopup);
        if (regular > 1)
        {
            text.Append(CultureInfo.InvariantCulture, $"# {regular} windows; showing {TextFormat.Quote(active.Title)}{(active.IsModal ? " (modal)" : string.Empty)}. Run windows to list them.\n");
        }

        text.Append(this.RenderWindow(active, request.Options));

        // Open menus and drop-downs live in separate popup windows; show them with the window they belong to.
        foreach (var popup in windows.Where(w => w.IsPopup && w != active))
        {
            text.Append("# popup\n").Append(this.RenderWindow(popup, request.Options));
        }
    }

    private string RenderWindow(WindowInfo window, SnapshotOptions options)
    {
        try
        {
            return this.Render(this.Driver.Capture(window.RuntimeId, SnapshotRenderer.MaxTreeDepth), options);
        }
        catch (ElementGoneException)
        {
            throw new AgentWpfException(ErrorCode.ActionFailed, "Window " + TextFormat.Quote(window.Title) + " closed during the snapshot.", "run snapshot again");
        }
    }

    private string Render(ElementSnapshot root, SnapshotOptions options)
        => SnapshotRenderer.Render(root, node => this.Session.Refs.Assign(node.RuntimeId), options);
}
