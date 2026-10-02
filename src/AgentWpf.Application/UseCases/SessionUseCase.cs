using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using AgentWpf.Application.Ports;
using AgentWpf.Application.Refs;
using AgentWpf.Application.Sessions;
using AgentWpf.Domain;

namespace AgentWpf.Application.UseCases;

/// <summary>
/// Base class for use cases that work on the target of a session.
/// </summary>
public abstract class SessionUseCase
{
    /// <summary>
    /// Initializes a new instance of the <see cref="SessionUseCase"/> class.
    /// </summary>
    /// <param name="session">The session.</param>
    /// <param name="driver">The automation driver.</param>
    protected SessionUseCase(AgentSession session, IAutomationDriver driver)
    {
        System.Diagnostics.Debug.Assert(session != null, "Precondition: session is required.");
        System.Diagnostics.Debug.Assert(driver != null, "Precondition: driver is required.");

        this.Session = session;
        this.Driver = driver;
    }

    /// <summary>
    /// Gets the session.
    /// </summary>
    protected AgentSession Session { get; }

    /// <summary>
    /// Gets the automation driver.
    /// </summary>
    protected IAutomationDriver Driver { get; }

    /// <summary>
    /// Returns the target process id or fails with a hint to open or attach an application.
    /// </summary>
    /// <returns>The target process id.</returns>
    protected int RequireTarget() => this.Session.TargetProcessId
        ?? throw new AgentWpfException(ErrorCode.ActionFailed, "No application attached.", "run open <exe> or attach --process <name>");

    /// <summary>
    /// Returns the windows of the target or fails when there are none.
    /// </summary>
    /// <returns>The windows, topmost first.</returns>
    protected IReadOnlyList<WindowInfo> RequireWindows()
    {
        var windows = this.Driver.GetWindows(this.RequireTarget());
        return windows.Count > 0
            ? windows
            : throw new AgentWpfException(ErrorCode.ActionFailed, "The application has no open window.", "wait --window <title> or open the app again");
    }

    /// <summary>
    /// Picks the window commands act on: a modal dialog first, then the window chosen by the agent,
    /// then the foreground window, then the topmost one.
    /// </summary>
    /// <param name="windows">The windows of the target, topmost first.</param>
    /// <returns>The active window.</returns>
    protected WindowInfo ResolveWindow(IReadOnlyList<WindowInfo> windows)
    {
        System.Diagnostics.Debug.Assert(windows.Count > 0, "Precondition: at least one window is required.");

        var candidates = windows.Where(w => !w.IsPopup).ToList();
        if (candidates.Count == 0)
        {
            return windows[0];
        }

        return candidates.FirstOrDefault(w => w.IsModal)
            ?? candidates.FirstOrDefault(w => w.RuntimeId == this.Session.ActiveWindowRuntimeId)
            ?? candidates.FirstOrDefault(w => w.IsForeground)
            ?? candidates[0];
    }

    /// <summary>
    /// Runs an operation on the element behind a ref and turns a vanished element into a stale-ref error.
    /// </summary>
    /// <typeparam name="T">The result type.</typeparam>
    /// <param name="refId">The ref given by the agent.</param>
    /// <param name="operation">The operation, receiving the runtime id.</param>
    /// <returns>The operation result.</returns>
    protected T OnElement<T>(RefId refId, Func<string, T> operation)
    {
        var runtimeId = this.Session.Refs.Resolve(refId);
        try
        {
            return operation(runtimeId);
        }
        catch (ElementGoneException)
        {
            this.Session.Refs.Forget(refId);
            throw RefRegistry.StaleRef(refId);
        }
    }

    /// <summary>
    /// Runs an operation on the element behind a ref and turns a vanished element into a stale-ref error.
    /// </summary>
    /// <param name="refId">The ref given by the agent.</param>
    /// <param name="operation">The operation, receiving the runtime id.</param>
    protected void OnElement(RefId refId, Action<string> operation)
        => this.OnElement(refId, runtimeId => { operation(runtimeId); return true; });

    /// <summary>
    /// Describes an element for error messages, e.g. <c>@e3 (text "Label")</c>.
    /// </summary>
    /// <param name="refId">The ref.</param>
    /// <param name="runtimeId">The runtime id.</param>
    /// <returns>The description.</returns>
    protected string Describe(RefId refId, string runtimeId)
    {
        var node = this.Driver.Capture(runtimeId, 0);
        var name = string.IsNullOrEmpty(node.Name) ? string.Empty : " " + TextFormat.Quote(node.Name);
        return string.Format(CultureInfo.InvariantCulture, "@{0} ({1}{2})", refId, node.Role.ToRoleName(), name);
    }
}
