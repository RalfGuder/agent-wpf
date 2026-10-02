using System;
using System.CommandLine;
using System.IO;
using AgentWpf.Application.UseCases;
using AgentWpf.Domain;

namespace AgentWpf.Cli;

/// <content>
/// The <c>open</c>, <c>attach</c>, <c>close</c>, <c>windows</c>, <c>window</c> and <c>snapshot</c> commands.
/// </content>
internal sealed partial class CommandLineApp
{
    private void AddLifecycleCommands(RootCommand root, Invocation inv)
    {
        var exe = new Argument<string>("exe") { Description = "Executable path or name on PATH." };
        var exeArgs = new Argument<string[]>("args") { Description = "Arguments for the app; put them after --.", Arity = ArgumentArity.ZeroOrMore };
        var open = new Command("open", "Start an app, make it the session target and wait for its first window.") { exe, exeArgs, inv.TimeoutOption };
        open.SetAction(pr => inv.Run(() => this.Services.Lifecycle.Open(
            ResolveExecutable(pr.GetValue(exe)!, inv), pr.GetValue(exeArgs) ?? [], inv.WorkingDirectory, Milliseconds(pr.GetValue(inv.TimeoutOption), DefaultOpenTimeout))));
        root.Subcommands.Add(open);

        var pid = new Option<int?>("--pid") { Description = "Process id." };
        var process = new Option<string?>("--process") { Description = "Executable name, e.g. notepad." };
        var title = new Option<string?>("--title") { Description = "Regular expression matched against window titles." };
        var attach = new Command("attach", "Attach to a running, allowlisted app.") { pid, process, title };
        attach.SetAction(pr => inv.Run(() => this.Services.Lifecycle.Attach(AttachTargetFrom(pr.GetValue(pid), pr.GetValue(process), pr.GetValue(title)))));
        root.Subcommands.Add(attach);

        var kill = new Option<bool>("--kill") { Description = "Terminate the app if it does not exit (e.g. unsaved-changes dialog)." };
        var close = new Command("close", "Close an app opened by this session, or detach from an attached app.") { kill };
        close.SetAction(pr => inv.Run(() => this.Services.Lifecycle.Close(pr.GetValue(kill))));
        root.Subcommands.Add(close);

        var windows = new Command("windows", "List the windows of the target with refs.");
        windows.SetAction(_ => inv.Run(() => this.Services.Lifecycle.Windows()));
        root.Subcommands.Add(windows);

        var windowRef = new Argument<string>("ref") { Description = "Window ref, e.g. @e5." };
        var window = new Command("window", "Make a window the default for snapshot and screenshot.") { windowRef };
        window.SetAction(pr => inv.Run(() => this.Services.Lifecycle.SwitchWindow(ParseRef(pr.GetValue(windowRef)))));
        root.Subcommands.Add(window);

        root.Subcommands.Add(this.SnapshotCommand(inv));
    }

    private Command SnapshotCommand(Invocation inv)
    {
        var interactive = new Option<bool>("--interactive", "-i") { Description = "Only actionable elements." };
        var compact = new Option<bool>("--compact", "-c") { Description = "Collapse unnamed layout containers." };
        var depth = new Option<int?>("--depth", "-d") { Description = "Maximum depth below the root." };
        var scope = new Option<string?>("--scope", "-s") { Description = "Only the subtree of this ref." };
        var allWindows = new Option<bool>("--all-windows") { Description = "Render every window of the app." };
        var verbose = new Option<bool>("--verbose") { Description = "Add class names and bounds." };
        var snapshot = new Command("snapshot", "Print the UI Automation tree of the active window with refs.") { interactive, compact, depth, scope, allWindows, verbose };
        snapshot.SetAction(pr => inv.Run(() => this.Services.Snapshot.Execute(new SnapshotRequest
        {
            Options = new Application.Snapshots.SnapshotOptions
            {
                InteractiveOnly = pr.GetValue(interactive),
                Compact = pr.GetValue(compact),
                MaxDepth = pr.GetValue(depth),
                Verbose = pr.GetValue(verbose),
            },
            Scope = ParseOptionalRef(pr.GetValue(scope)),
            AllWindows = pr.GetValue(allWindows),
        })));
        return snapshot;
    }

    private static AttachTarget AttachTargetFrom(int? pid, string? process, string? title)
    {
        var given = (pid.HasValue ? 1 : 0) + (process is null ? 0 : 1) + (title is null ? 0 : 1);
        if (given != 1)
        {
            throw new AgentWpfException(ErrorCode.Usage, "attach needs exactly one of --pid, --process or --title.", "e.g. attach --process notepad");
        }

        return pid is int id ? AttachTarget.ForProcessId(id)
            : process is not null ? AttachTarget.ForProcessName(process)
            : AttachTarget.ForTitle(title!);
    }

    private static string ResolveExecutable(string exe, Invocation inv)
    {
        var candidate = inv.ResolvePath(exe);
        return Path.IsPathRooted(exe) || File.Exists(candidate) ? candidate : exe;
    }
}
