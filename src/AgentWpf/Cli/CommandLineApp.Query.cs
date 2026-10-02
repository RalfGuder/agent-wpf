using System;
using System.CommandLine;
using System.Globalization;
using AgentWpf.Application.UseCases;
using AgentWpf.Domain;

namespace AgentWpf.Cli;

/// <content>
/// The <c>get</c>, <c>table</c>, <c>wait</c> and <c>screenshot</c> commands.
/// </content>
internal sealed partial class CommandLineApp
{
    private static readonly TimeSpan DefaultIdleQuiet = TimeSpan.FromMilliseconds(500);

    private void AddQueryCommands(RootCommand root, Invocation inv)
    {
        var get = new Command("get", "Read element data.");
        get.Subcommands.Add(RefCommand("text", "Print the element's name.", inv, [], (_, r) => this.Services.Query.GetText(r)));
        get.Subcommands.Add(RefCommand("value", "Print the element's value.", inv, [], (_, r) => this.Services.Query.GetValue(r)));
        root.Subcommands.Add(get);

        var rows = new Option<string>("--rows") { Description = "Row range first-last, zero-based.", DefaultValueFactory = _ => "0-49" };
        root.Subcommands.Add(RefCommand("table", "Print rows of a data grid, realizing virtualized rows.", inv, [rows], (pr, r) =>
        {
            var (first, last) = ParseRange(pr.GetValue(rows)!);
            return this.Services.Query.Table(r, first, last);
        }));

        root.Subcommands.Add(this.WaitCommand(inv));

        var path = new Argument<string?>("path") { Description = "PNG path; default is a temp file.", Arity = ArgumentArity.ZeroOrOne };
        var element = new Option<string?>("--element") { Description = "Crop to this element's ref." };
        var screenshot = new Command("screenshot", "Save a PNG of the active window, even when covered.") { path, element };
        screenshot.SetAction(pr => inv.Run(() =>
        {
            var target = pr.GetValue(path) is string p
                ? inv.ResolvePath(p)
                : System.IO.Path.Combine(System.IO.Path.GetTempPath(), "agent-wpf-" + DateTime.Now.ToString("yyyyMMdd-HHmmss-fff", CultureInfo.InvariantCulture) + ".png");
            return this.Services.Screenshot.Execute(target, ParseOptionalRef(pr.GetValue(element)));
        }));
        root.Subcommands.Add(screenshot);
    }

    private Command WaitCommand(Invocation inv)
    {
        var target = new Argument<string?>("target") { Description = "A ref (@e3) to wait for, or milliseconds to sleep.", Arity = ArgumentArity.ZeroOrOne };
        var state = new Option<string>("--state") { Description = "visible, hidden, enabled or disabled.", DefaultValueFactory = _ => "visible" };
        var text = new Option<string?>("--text") { Description = "Wait until this text appears in any element." };
        var window = new Option<string?>("--window") { Description = "Wait for a window whose title matches this regex and activate it." };
        var idle = new Option<int?>("--idle") { Description = "Wait until the UI structure is quiet for N ms (default 500).", Arity = ArgumentArity.ZeroOrOne };
        var wait = new Command("wait", "Wait for an element state, text, window, idle UI or a fixed time.") { target, state, text, window, idle, inv.TimeoutOption };
        wait.SetAction(pr => inv.Run(() =>
        {
            var timeout = Milliseconds(pr.GetValue(inv.TimeoutOption), DefaultWaitTimeout);
            var idleGiven = pr.GetResult(idle) is not null;
            var given = (pr.GetValue(target) is null ? 0 : 1) + (pr.GetValue(text) is null ? 0 : 1) + (pr.GetValue(window) is null ? 0 : 1) + (idleGiven ? 1 : 0);
            if (given != 1)
            {
                throw new AgentWpfException(ErrorCode.Usage, "wait needs exactly one of <ref>, <ms>, --text, --window or --idle.", "e.g. wait @e3, wait 500, wait --text Done");
            }

            return idleGiven ? this.Services.Wait.ForIdle(Milliseconds(pr.GetValue(idle), DefaultIdleQuiet), timeout)
                : pr.GetValue(text) is string t ? this.Services.Wait.ForText(t, timeout)
                : pr.GetValue(window) is string w ? this.Services.Wait.ForWindow(w, timeout)
                : this.WaitForTarget(pr.GetValue(target)!, pr.GetValue(state)!, timeout);
        }));
        return wait;
    }

    private CommandResult WaitForTarget(string target, string state, TimeSpan timeout)
    {
        if (int.TryParse(target, NumberStyles.None, CultureInfo.InvariantCulture, out var ms))
        {
            return this.Services.Wait.Sleep(TimeSpan.FromMilliseconds(ms));
        }

        if (!Enum.TryParse<ElementWaitState>(state, ignoreCase: true, out var waitState))
        {
            throw new AgentWpfException(ErrorCode.Usage, "'" + state + "' is not a state.", "use visible, hidden, enabled or disabled");
        }

        return this.Services.Wait.ForElement(ParseRef(target), waitState, timeout);
    }

    private static (int First, int Last) ParseRange(string text)
    {
        var parts = text.Split('-');
        if (parts.Length == 2
            && int.TryParse(parts[0], NumberStyles.None, CultureInfo.InvariantCulture, out var first)
            && int.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out var last))
        {
            return (first, last);
        }

        throw new AgentWpfException(ErrorCode.Usage, "'" + text + "' is not a row range.", "use --rows 0-49");
    }
}
