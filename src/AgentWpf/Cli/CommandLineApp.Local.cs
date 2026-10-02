using System.CommandLine;
using AgentWpf.Application.UseCases;

namespace AgentWpf.Cli;

/// <content>
/// Commands that need no target: <c>skills</c> and the hidden <c>shutdown</c>.
/// </content>
internal sealed partial class CommandLineApp
{
    /// <summary>
    /// Command names that the CLI executes in-process without contacting a daemon.
    /// </summary>
    public static readonly string[] LocalCommands = ["skills", "--help", "-h", "-?", "--version"];

    private void AddLocalCommands(RootCommand root, Invocation inv)
    {
        var skills = new Command("skills", "Print usage guides for AI agents.");
        var list = new Command("list", "List the available guides.");
        list.SetAction(_ => inv.Run(() => new CommandResult(string.Join('\n', SkillCatalog.Names()) + "\n")));
        var name = new Argument<string>("name") { Description = "Guide name, e.g. core." };
        var full = new Option<bool>("--full") { Description = "Include all reference documents." };
        var get = new Command("get", "Print a guide.") { name, full };
        get.SetAction(pr => inv.Run(() => new CommandResult(SkillCatalog.Get(pr.GetValue(name)!, pr.GetValue(full)))));
        skills.Subcommands.Add(list);
        skills.Subcommands.Add(get);
        root.Subcommands.Add(skills);

        var shutdown = new Command("shutdown", "Stop the session daemon.") { Hidden = true };
        shutdown.SetAction(_ => inv.Run(() =>
        {
            this.ShutdownRequested = true;
            return new CommandResult("daemon stopping\n");
        }));
        root.Subcommands.Add(shutdown);
    }
}
