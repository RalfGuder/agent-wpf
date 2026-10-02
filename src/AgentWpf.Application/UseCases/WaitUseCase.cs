using System;
using System.Collections.Generic;
using System.Globalization;
using AgentWpf.Application.Ports;
using AgentWpf.Application.Sessions;
using AgentWpf.Application.Snapshots;
using AgentWpf.Domain;

namespace AgentWpf.Application.UseCases;

/// <summary>
/// The element state <c>wait @eN --state</c> waits for.
/// </summary>
public enum ElementWaitState
{
    /// <summary>The element exists and is on screen.</summary>
    Visible,

    /// <summary>The element is gone or off screen.</summary>
    Hidden,

    /// <summary>The element is enabled.</summary>
    Enabled,

    /// <summary>The element is disabled.</summary>
    Disabled,
}

/// <summary>
/// The <c>wait</c> command: for an element state, a text, a window, UI idleness or a fixed time.
/// </summary>
public sealed class WaitUseCase : SessionUseCase
{
    // Upper bound of elements searched by wait --text per poll.
    private const int MaxSearchedNodes = 20_000;

    private readonly IClock clock;

    /// <summary>
    /// Initializes a new instance of the <see cref="WaitUseCase"/> class.
    /// </summary>
    /// <param name="session">The session.</param>
    /// <param name="driver">The automation driver.</param>
    /// <param name="clock">The clock.</param>
    public WaitUseCase(AgentSession session, IAutomationDriver driver, IClock clock)
        : base(session, driver)
    {
        this.clock = clock;
    }

    /// <summary>
    /// Waits a fixed time.
    /// </summary>
    /// <param name="duration">The duration.</param>
    /// <returns>The result.</returns>
    public CommandResult Sleep(TimeSpan duration)
    {
        System.Diagnostics.Debug.Assert(duration >= TimeSpan.Zero, "Precondition: duration must not be negative.");
        System.Diagnostics.Debug.Assert(duration <= TimeSpan.FromHours(1), "Precondition: duration must be at most one hour.");

        this.clock.Sleep(duration);
        return new CommandResult(string.Format(CultureInfo.InvariantCulture, "waited {0}ms\n", (long)duration.TotalMilliseconds));
    }

    /// <summary>
    /// Waits until an element reaches a state.
    /// </summary>
    /// <param name="refId">The element.</param>
    /// <param name="state">The desired state.</param>
    /// <param name="timeout">The timeout.</param>
    /// <returns>The result.</returns>
    public CommandResult ForElement(RefId refId, ElementWaitState state, TimeSpan timeout)
    {
        var runtimeId = this.Session.Refs.Resolve(refId);
        var ok = Poller.Until(this.clock, timeout, () => this.IsInState(refId, runtimeId, state));
        var label = state switch { ElementWaitState.Visible => "visible", ElementWaitState.Hidden => "hidden", ElementWaitState.Enabled => "enabled", _ => "disabled" };
        if (!ok)
        {
            throw TimeoutError(string.Format(CultureInfo.InvariantCulture, "@{0} did not become {1}", refId, label), timeout);
        }

        return new CommandResult(string.Format(CultureInfo.InvariantCulture, "@{0} is {1}\n", refId, label));
    }

    /// <summary>
    /// Waits until any window of the target shows a text in an element name or value.
    /// </summary>
    /// <param name="text">The text, matched case-insensitively as a substring.</param>
    /// <param name="timeout">The timeout.</param>
    /// <returns>The result.</returns>
    public CommandResult ForText(string text, TimeSpan timeout)
    {
        System.Diagnostics.Debug.Assert(!string.IsNullOrEmpty(text), "Precondition: text must not be empty.");

        var pid = this.RequireTarget();
        if (!Poller.Until(this.clock, timeout, () => this.ContainsText(pid, text)))
        {
            throw TimeoutError("Text " + TextFormat.Quote(text) + " did not appear", timeout);
        }

        return new CommandResult("found " + TextFormat.Quote(text) + "\n");
    }

    /// <summary>
    /// Waits until a window whose title matches a pattern is open and makes it the active window.
    /// </summary>
    /// <param name="titlePattern">The regular expression matched against window titles.</param>
    /// <param name="timeout">The timeout.</param>
    /// <returns>The result naming the window and its ref.</returns>
    public CommandResult ForWindow(string titlePattern, TimeSpan timeout)
    {
        var regex = Patterns.Compile(titlePattern);
        var pid = this.RequireTarget();
        WindowInfo? match = null;
        if (!Poller.Until(this.clock, timeout, () => (match = FindWindow(this.Driver.GetWindows(pid), regex)) != null))
        {
            throw TimeoutError("No window matching /" + titlePattern + "/ appeared", timeout);
        }

        this.Session.ActiveWindowRuntimeId = match!.RuntimeId;

        System.Diagnostics.Debug.Assert(this.Session.ActiveWindowRuntimeId != null, "Postcondition: a window is active.");
        return new CommandResult(string.Format(CultureInfo.InvariantCulture, "window {0} [ref={1}]\n", TextFormat.Quote(match.Title), this.Session.Refs.Assign(match.RuntimeId)));
    }

    /// <summary>
    /// Waits until the target's UI structure has not changed for a quiet period.
    /// </summary>
    /// <param name="quiet">The required period without structure changes.</param>
    /// <param name="timeout">The timeout.</param>
    /// <returns>The result.</returns>
    public CommandResult ForIdle(TimeSpan quiet, TimeSpan timeout)
    {
        System.Diagnostics.Debug.Assert(quiet > TimeSpan.Zero, "Precondition: quiet period must be positive.");

        var pid = this.RequireTarget();
        var last = this.Driver.GetStructureChangeCount(pid);
        var lastChange = this.clock.UtcNow;
        var idle = Poller.Until(this.clock, timeout, () =>
        {
            var current = this.Driver.GetStructureChangeCount(pid);
            if (current != last)
            {
                last = current;
                lastChange = this.clock.UtcNow;
            }

            return this.clock.UtcNow - lastChange >= quiet;
        });

        return idle ? new CommandResult("idle\n") : throw TimeoutError("The UI kept changing", timeout);
    }

    private static AgentWpfException TimeoutError(string what, TimeSpan timeout) => new(
        ErrorCode.Timeout,
        string.Format(CultureInfo.InvariantCulture, "{0} within {1}ms.", what, (long)timeout.TotalMilliseconds),
        "take a snapshot to see the current state, or raise --timeout");

    private static WindowInfo? FindWindow(IReadOnlyList<WindowInfo> windows, System.Text.RegularExpressions.Regex regex)
    {
        foreach (var window in windows)
        {
            if (regex.IsMatch(window.Title))
            {
                return window;
            }
        }

        return null;
    }

    private bool IsInState(RefId refId, string runtimeId, ElementWaitState state)
    {
        ElementSnapshot node;
        try
        {
            node = this.Driver.Capture(runtimeId, 0);
        }
        catch (ElementGoneException)
        {
            if (state == ElementWaitState.Hidden)
            {
                return true;
            }

            this.Session.Refs.Forget(refId);
            throw Refs.RefRegistry.StaleRef(refId);
        }

        return state switch
        {
            ElementWaitState.Visible => !node.States.HasFlag(ElementState.Offscreen),
            ElementWaitState.Hidden => node.States.HasFlag(ElementState.Offscreen),
            ElementWaitState.Enabled => !node.States.HasFlag(ElementState.Disabled),
            _ => node.States.HasFlag(ElementState.Disabled),
        };
    }

    private bool ContainsText(int pid, string text)
    {
        foreach (var window in this.Driver.GetWindows(pid))
        {
            try
            {
                if (TreeContains(this.Driver.Capture(window.RuntimeId, SnapshotRenderer.MaxTreeDepth), text))
                {
                    return true;
                }
            }
            catch (ElementGoneException)
            {
                // The window closed while searching; the next poll sees the new window list.
            }
        }

        return false;
    }

    private static bool TreeContains(ElementSnapshot root, string text)
    {
        var stack = new Stack<ElementSnapshot>();
        stack.Push(root);
        for (var visited = 0; stack.Count > 0 && visited < MaxSearchedNodes; visited++)
        {
            var node = stack.Pop();
            if ((node.Name?.Contains(text, StringComparison.OrdinalIgnoreCase) ?? false)
                || (node.Value?.Contains(text, StringComparison.OrdinalIgnoreCase) ?? false))
            {
                return true;
            }

            foreach (var child in node.Children)
            {
                stack.Push(child);
            }
        }

        return false;
    }
}
