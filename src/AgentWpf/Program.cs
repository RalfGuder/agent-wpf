using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using AgentWpf.Application.Sessions;
using AgentWpf.Cli;
using AgentWpf.Infrastructure.FlaUI;
using AgentWpf.Protocol;

namespace AgentWpf;

/// <summary>
/// The agent-wpf entry point: a thin client that forwards commands to a per-session daemon, which it
/// starts on first use, and the daemon itself (<c>agent-wpf daemon</c>).
/// </summary>
internal static class Program
{
    private const string SessionVariable = "AGENT_WPF_SESSION";

    private const string AllowVariable = "AGENT_WPF_ALLOW_PROCESSES";

    private static readonly TimeSpan IdleTimeout = TimeSpan.FromHours(1);

    private static readonly TimeSpan StartTimeout = TimeSpan.FromSeconds(10);

    // Long enough for the slowest wait command that may be running in the daemon.
    private static readonly TimeSpan BusyTimeout = TimeSpan.FromMinutes(10);

    private static async Task<int> Main(string[] args)
    {
        Console.OutputEncoding = Encoding.UTF8;
        var options = ClientOptions.Parse(args);
        if (options.Args.Count > 0 && options.Args[0] == "daemon")
        {
            return await RunDaemonAsync(options);
        }

        if (options.Args.Count == 0 || CommandLineApp.LocalCommands.Contains(options.Args[0]))
        {
            return await WriteAsync(new CommandLineApp(() => throw new InvalidOperationException("Local commands need no session.")).Execute(options.Args, Environment.CurrentDirectory));
        }

        if (options.Args[0] == "session")
        {
            return await SessionCommandAsync(options);
        }

        return await WriteAsync(await SendAsync(options, options.Args));
    }

    private static async Task<DaemonResponse> SendAsync(ClientOptions options, IReadOnlyList<string> args)
    {
        var pipe = PipeNames.ForSession(Environment.UserName, options.Session);
        var running = DaemonClient.IsRunning(pipe);
        if (!running)
        {
            DaemonClient.StartDaemon(options.Session, options.AllowedProcesses);
        }
        else if (options.AllowedProcesses.Count > 0)
        {
            await Console.Error.WriteLineAsync("warning: --allow-process only applies when a session starts; run agent-wpf session stop first.");
        }

        try
        {
            return await DaemonClient.SendAsync(pipe, new DaemonRequest(args, Environment.CurrentDirectory), running ? BusyTimeout : StartTimeout, CancellationToken.None);
        }
        catch (TimeoutException)
        {
            return new DaemonResponse(1, string.Empty, "error: The session daemon did not answer.\nhint: run agent-wpf session stop and try again\n");
        }
    }

    private static async Task<int> SessionCommandAsync(ClientOptions options)
    {
        var sub = options.Args.Count > 1 ? options.Args[1] : null;
        var prefix = PipeNames.ForSession(Environment.UserName, "x")[..^1];
        if (sub == "list")
        {
            var sessions = DaemonClient.ListPipes(prefix).Select(p => p[prefix.Length..]);
            await Console.Out.WriteAsync(string.Concat(sessions.Select(s => s + "\n")));
            return 0;
        }

        if (sub == "stop")
        {
            var pipe = PipeNames.ForSession(Environment.UserName, options.Session);
            return await WriteAsync(DaemonClient.IsRunning(pipe) ? await SendAsync(options, ["shutdown"]) : new DaemonResponse(0, "no daemon for session " + options.Session + "\n", string.Empty));
        }

        return await WriteAsync(new DaemonResponse(2, string.Empty, "error: Unknown session command.\nhint: use session list or session stop\n"));
    }

    private static async Task<int> RunDaemonAsync(ClientOptions options)
    {
        NativeMethodsBootstrap.EnableDpiAwareness();
        using var driver = new FlaUiAutomationDriver();
        var session = new AgentSession(options.Session);
        foreach (var name in options.AllowedProcesses.Concat(AllowedFromEnvironment()))
        {
            session.Allowlist.AllowProcessName(name);
        }

        var services = new SessionServices(session, driver, new ProcessLauncher(), new PrintWindowCapture(), new SystemClock());
        var app = new CommandLineApp(() => services);
        var host = new DaemonHost(PipeNames.ForSession(Environment.UserName, options.Session), r => app.Execute(r.Args, r.WorkingDirectory), IdleTimeout, () => app.ShutdownRequested);
        try
        {
            await host.RunAsync(CancellationToken.None);
            return 0;
        }
        catch (IOException)
        {
            // Another daemon already serves this session.
            return 0;
        }
    }

    private static string[] AllowedFromEnvironment()
        => (Environment.GetEnvironmentVariable(AllowVariable) ?? string.Empty).Split([';', ','], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    private static async Task<int> WriteAsync(DaemonResponse response)
    {
        await Console.Out.WriteAsync(response.Stdout);
        await Console.Error.WriteAsync(response.Stderr);
        return response.ExitCode;
    }

    // Client-side global options, removed before the arguments are sent to the daemon.
    private sealed record ClientOptions(string Session, IReadOnlyList<string> AllowedProcesses, IReadOnlyList<string> Args)
    {
        public static ClientOptions Parse(string[] args)
        {
            var session = Environment.GetEnvironmentVariable(SessionVariable) is { Length: > 0 } s ? s : "default";
            var allowed = new List<string>();
            var rest = new List<string>();
            for (var i = 0; i < args.Length; i++)
            {
                if (args[i] is "--session" && i + 1 < args.Length)
                {
                    session = args[++i];
                }
                else if (args[i] is "--allow-process" && i + 1 < args.Length)
                {
                    allowed.Add(args[++i]);
                }
                else
                {
                    rest.Add(args[i]);
                }
            }

            return new ClientOptions(session.ToLower(CultureInfo.InvariantCulture), allowed, rest);
        }
    }
}
