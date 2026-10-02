using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using AgentWpf.Application.Ports;
using AgentWpf.Application.Sessions;
using AgentWpf.Domain;

namespace AgentWpf.Application.UseCases;

/// <summary>
/// Opening, attaching, closing and window management: <c>open</c>, <c>attach</c>, <c>close</c>,
/// <c>windows</c> and <c>window</c>.
/// </summary>
public sealed class LifecycleUseCases : SessionUseCase
{
    // How long close waits for a gracefully closed process to exit.
    private static readonly TimeSpan ExitTimeout = TimeSpan.FromSeconds(5);

    private readonly IProcessLauncher launcher;

    private readonly IClock clock;

    /// <summary>
    /// Initializes a new instance of the <see cref="LifecycleUseCases"/> class.
    /// </summary>
    /// <param name="session">The session.</param>
    /// <param name="driver">The automation driver.</param>
    /// <param name="launcher">The process launcher.</param>
    /// <param name="clock">The clock.</param>
    public LifecycleUseCases(AgentSession session, IAutomationDriver driver, IProcessLauncher launcher, IClock clock)
        : base(session, driver)
    {
        this.launcher = launcher;
        this.clock = clock;
    }

    /// <summary>
    /// Starts an application, makes it the session target and waits for its first window.
    /// </summary>
    /// <param name="path">The executable path or name.</param>
    /// <param name="arguments">The command line arguments.</param>
    /// <param name="workingDirectory">The working directory.</param>
    /// <param name="timeout">How long to wait for the first window.</param>
    /// <returns>The result naming process and window.</returns>
    public CommandResult Open(string path, IReadOnlyList<string> arguments, string workingDirectory, TimeSpan timeout)
    {
        System.Diagnostics.Debug.Assert(!string.IsNullOrWhiteSpace(path), "Precondition: path must not be empty.");
        System.Diagnostics.Debug.Assert(arguments != null, "Precondition: arguments must not be null.");

        this.Session.Detach();
        var pid = this.launcher.Start(path, arguments, workingDirectory);
        var info = this.Driver.GetProcess(pid);
        EnsureNotElevated(info);
        this.Session.AttachTo(pid, launchedBySession: true);

        IReadOnlyList<WindowInfo> windows = [];
        if (!Poller.Until(this.clock, timeout, () => (windows = this.Driver.GetWindows(pid)).Count > 0))
        {
            throw new AgentWpfException(
                ErrorCode.Timeout,
                string.Format(CultureInfo.InvariantCulture, "{0} (pid {1}) showed no window within {2:0.#}s.", info.Name, pid, timeout.TotalSeconds),
                "wait --window <title> --timeout <ms>, or check that the app starts at all");
        }

        System.Diagnostics.Debug.Assert(this.Session.TargetProcessId == pid, "Postcondition: launched process is the target.");
        return new CommandResult(string.Format(
            CultureInfo.InvariantCulture, "opened {0} (pid {1}); window {2}\n", info.Name, pid, TextFormat.Quote(this.ResolveWindow(windows).Title)));
    }

    /// <summary>
    /// Makes a running, allowlisted process the session target.
    /// </summary>
    /// <param name="target">How to find the process.</param>
    /// <returns>The result naming the process.</returns>
    public CommandResult Attach(AttachTarget target)
    {
        System.Diagnostics.Debug.Assert(target != null, "Precondition: target must not be null.");

        var pid = this.ResolveProcessId(target);
        var info = this.Driver.GetProcess(pid);
        if (!this.Session.Allowlist.IsAllowed(info.Name, pid))
        {
            throw new AgentWpfException(
                ErrorCode.NotAllowed,
                string.Format(CultureInfo.InvariantCulture, "Process {0} (pid {1}) is not on the allowlist.", info.Name, pid),
                string.Format(CultureInfo.InvariantCulture, "ask the user to start the session with --allow-process {0}, or launch the app with agent-wpf open", info.Name));
        }

        EnsureNotElevated(info);
        this.Session.AttachTo(pid, launchedBySession: false);

        System.Diagnostics.Debug.Assert(this.Session.TargetProcessId == pid, "Postcondition: process is the target.");
        return new CommandResult(string.Format(CultureInfo.InvariantCulture, "attached to {0} (pid {1})\n", info.Name, pid));
    }

    /// <summary>
    /// Closes a launched application gracefully (or kills it), or detaches from an attached one.
    /// </summary>
    /// <param name="kill">Whether to terminate a launched process that does not exit by itself.</param>
    /// <returns>The result.</returns>
    public CommandResult Close(bool kill)
    {
        if (this.Session.TargetProcessId is not int pid)
        {
            return new CommandResult("nothing attached\n");
        }

        if (!this.Session.LaunchedBySession)
        {
            this.Session.Detach();
            return new CommandResult(string.Format(CultureInfo.InvariantCulture, "detached from pid {0}\n", pid));
        }

        foreach (var window in this.Driver.GetWindows(pid))
        {
            this.TryCloseWindow(window);
        }

        var exited = this.launcher.WaitForExit(pid, ExitTimeout);
        if (!exited && !kill)
        {
            throw new AgentWpfException(
                ErrorCode.ActionFailed,
                string.Format(CultureInfo.InvariantCulture, "pid {0} is still running.", pid),
                "a dialog may ask to save changes: snapshot and answer it, or close --kill");
        }

        if (!exited)
        {
            this.launcher.Kill(pid);
        }

        this.Session.Detach();

        System.Diagnostics.Debug.Assert(this.Session.TargetProcessId is null, "Postcondition: session has no target.");
        return new CommandResult(string.Format(CultureInfo.InvariantCulture, exited ? "closed pid {0}\n" : "killed pid {0}\n", pid));
    }

    /// <summary>
    /// Lists the windows of the target with refs, marking modal and active windows.
    /// </summary>
    /// <returns>The window list.</returns>
    public CommandResult Windows()
    {
        var windows = this.Driver.GetWindows(this.RequireTarget());
        if (windows.Count == 0)
        {
            return new CommandResult("no windows\n");
        }

        var active = this.ResolveWindow(windows);
        var text = new StringBuilder();
        foreach (var window in windows)
        {
            text.Append("- window ").Append(TextFormat.Quote(window.Title))
                .Append(" [ref=").Append(this.Session.Refs.Assign(window.RuntimeId)).Append(']')
                .Append(window.IsModal ? " [modal]" : string.Empty)
                .Append(window.IsPopup ? " [popup]" : string.Empty)
                .Append(window == active ? " [active]" : string.Empty)
                .Append('\n');
        }

        System.Diagnostics.Debug.Assert(text.Length > 0, "Postcondition: at least one line is rendered.");
        return new CommandResult(text.ToString());
    }

    /// <summary>
    /// Makes a window the one snapshots and screenshots use by default.
    /// </summary>
    /// <param name="windowRef">The ref of a window from <c>windows</c> or a snapshot.</param>
    /// <returns>The result naming the window.</returns>
    public CommandResult SwitchWindow(RefId windowRef)
    {
        var runtimeId = this.Session.Refs.Resolve(windowRef);
        var window = this.Driver.GetWindows(this.RequireTarget()).FirstOrDefault(w => w.RuntimeId == runtimeId)
            ?? throw new AgentWpfException(ErrorCode.Usage, string.Format(CultureInfo.InvariantCulture, "@{0} is not an open window of the target.", windowRef), "run windows to list the windows");

        this.Session.ActiveWindowRuntimeId = runtimeId;

        System.Diagnostics.Debug.Assert(this.Session.ActiveWindowRuntimeId == window.RuntimeId, "Postcondition: window is active.");
        return new CommandResult("active window " + TextFormat.Quote(window.Title) + "\n");
    }

    private static void EnsureNotElevated(ProcessInfo info)
    {
        if (info.IsElevated)
        {
            throw new AgentWpfException(
                ErrorCode.AccessDenied,
                string.Format(CultureInfo.InvariantCulture, "{0} (pid {1}) runs elevated; Windows blocks automation from a normal process.", info.Name, info.ProcessId),
                "ask the user to run agent-wpf from an elevated shell");
        }
    }

    private int ResolveProcessId(AttachTarget target)
    {
        if (target.ProcessId is int pid)
        {
            return pid;
        }

        if (target.ProcessName is string name)
        {
            return Single(this.launcher.FindByName(name).Select(id => (id, string.Empty)).ToList(), "named " + name);
        }

        var regex = Patterns.Compile(target.TitlePattern!);
        var matches = this.Driver.GetDesktopWindows()
            .Where(w => regex.IsMatch(w.Title))
            .GroupBy(w => w.ProcessId)
            .Select(g => (g.Key, TextFormat.Quote(g.First().Title)))
            .ToList();
        return Single(matches, "with a window titled /" + target.TitlePattern + "/");
    }

    private static int Single(List<(int Pid, string Label)> candidates, string description)
    {
        if (candidates.Count == 1)
        {
            return candidates[0].Pid;
        }

        if (candidates.Count == 0)
        {
            throw new AgentWpfException(ErrorCode.ActionFailed, "No running process " + description + ".", "check the name, or open the app with agent-wpf open");
        }

        var list = string.Join(", ", candidates.Select(c => string.Format(CultureInfo.InvariantCulture, "{0} {1}", c.Pid, c.Label).TrimEnd()));
        throw new AgentWpfException(ErrorCode.ActionFailed, "Several processes " + description + ": " + list + ".", "attach --pid <id>");
    }

    private void TryCloseWindow(WindowInfo window)
    {
        try
        {
            this.Driver.CloseWindow(window.RuntimeId);
        }
        catch (ElementGoneException)
        {
            // The window closed on its own, e.g. together with its owner.
        }
        catch (AgentWpfException)
        {
            // Popups and some dialogs cannot be closed via WindowPattern; the exit check below decides.
        }
    }
}
