using System;
using System.Collections.Generic;
using System.CommandLine;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using AgentWpf.Application.Sessions;
using AgentWpf.Application.UseCases;
using AgentWpf.Domain;
using AgentWpf.Protocol;

namespace AgentWpf.Cli;

/// <summary>
/// Parses an agent-wpf command line and executes it against a session. Runs inside the daemon for
/// session commands and inside the CLI for local commands such as <c>--help</c> and <c>skills</c>.
/// </summary>
internal sealed partial class CommandLineApp
{
    private static readonly TimeSpan DefaultWaitTimeout = TimeSpan.FromSeconds(10);

    private static readonly TimeSpan DefaultOpenTimeout = TimeSpan.FromSeconds(30);

    private readonly Func<SessionServices> servicesFactory;

    private SessionServices? services;

    /// <summary>
    /// Initializes a new instance of the <see cref="CommandLineApp"/> class.
    /// </summary>
    /// <param name="servicesFactory">Creates the session services on first use; local commands never call it.</param>
    public CommandLineApp(Func<SessionServices> servicesFactory)
    {
        Debug.Assert(servicesFactory != null, "Precondition: servicesFactory is required.");

        this.servicesFactory = servicesFactory;

        Debug.Assert(!this.ShutdownRequested, "Postcondition: a new app is not shutting down.");
    }

    /// <summary>
    /// Gets a value indicating whether the <c>shutdown</c> command was executed.
    /// </summary>
    public bool ShutdownRequested { get; private set; }

    private SessionServices Services => this.services ??= this.servicesFactory();

    /// <summary>
    /// Parses and executes one command line. Never throws for expected or unexpected command failures.
    /// </summary>
    /// <param name="args">The arguments.</param>
    /// <param name="workingDirectory">The caller's working directory for relative paths.</param>
    /// <returns>The exit code and output.</returns>
    public DaemonResponse Execute(IReadOnlyList<string> args, string workingDirectory)
    {
        Debug.Assert(args != null, "Precondition: args must not be null.");
        Debug.Assert(!string.IsNullOrEmpty(workingDirectory), "Precondition: workingDirectory must not be empty.");

        var invocation = new Invocation(workingDirectory);
        var root = this.BuildRoot(invocation);
        // "@e3" is a ref, not a response file.
        var parse = root.Parse(args, new ParserConfiguration { ResponseFileTokenReplacer = null });
        invocation.Json = parse.GetValue(invocation.JsonOption);
        if (parse.Errors.Count > 0)
        {
            return invocation.Fail(new AgentWpfException(ErrorCode.Usage, string.Join("; ", parse.Errors.Select(e => e.Message)), "run agent-wpf --help"));
        }

        using var stdout = new StringWriter(CultureInfo.InvariantCulture);
        using var stderr = new StringWriter(CultureInfo.InvariantCulture);
        var code = parse.Invoke(new InvocationConfiguration { Output = stdout, Error = stderr, EnableDefaultExceptionHandler = false });
        var response = invocation.Response ?? new DaemonResponse(code, stdout.ToString(), stderr.ToString());

        Debug.Assert(response.ExitCode >= 0, "Postcondition: exit codes are never negative.");
        return response;
    }

    private static RefId ParseRef(string? text)
    {
        if (RefId.TryParse(text, out var refId))
        {
            return refId;
        }

        throw new AgentWpfException(ErrorCode.Usage, "'" + text + "' is not a ref.", "use a ref like @e3 from the latest snapshot");
    }

    private static RefId? ParseOptionalRef(string? text) => text is null ? null : ParseRef(text);

    private static TimeSpan Milliseconds(int? value, TimeSpan fallback) => value is int ms
        ? (ms >= 0 ? TimeSpan.FromMilliseconds(ms) : throw new AgentWpfException(ErrorCode.Usage, "Timeouts must not be negative.", null))
        : fallback;

    private RootCommand BuildRoot(Invocation invocation)
    {
        var root = new RootCommand("agent-wpf: drive Windows desktop apps (WPF, WinForms, Win32) through UI Automation.");
        root.Options.Add(invocation.JsonOption);
        this.AddLifecycleCommands(root, invocation);
        this.AddInteractionCommands(root, invocation);
        this.AddQueryCommands(root, invocation);
        this.AddLocalCommands(root, invocation);
        return root;
    }

    // Per-call state: output mode, working directory and the produced response.
    private sealed class Invocation(string workingDirectory)
    {
        public Option<bool> JsonOption { get; } = new("--json") { Description = "Print a JSON envelope {ok, data, error}.", Recursive = true };

        public Option<int?> TimeoutOption { get; } = new("--timeout") { Description = "Timeout in milliseconds." };

        public string WorkingDirectory { get; } = workingDirectory;

        public bool Json { get; set; }

        public DaemonResponse? Response { get; private set; }

        public string ResolvePath(string path) => Path.GetFullPath(path, this.WorkingDirectory);

        public int Run(Func<CommandResult> action)
        {
            try
            {
                this.Response = OutputFormatter.Success(action(), this.Json);
            }
            catch (AgentWpfException ex)
            {
                this.Response = this.Fail(ex);
            }
            catch (Exception ex) when (ex is not OutOfMemoryException)
            {
                this.Response = this.Fail(new AgentWpfException(ErrorCode.ActionFailed, ex.GetType().Name + ": " + ex.Message, "take a snapshot; if this repeats, restart with agent-wpf session stop"));
            }

            return this.Response.ExitCode;
        }

        public DaemonResponse Fail(AgentWpfException ex) => this.Response = OutputFormatter.Failure(ex, this.Json);
    }
}
