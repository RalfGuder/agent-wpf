using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AgentWpf.Protocol;

namespace AgentWpf.Cli;

/// <summary>
/// Connects the CLI to a session daemon, starting the daemon on first use.
/// </summary>
internal static class DaemonClient
{
    private const string PipeDirectory = @"\\.\pipe\";

    /// <summary>
    /// Determines whether a daemon serves the pipe, without connecting to it.
    /// </summary>
    /// <param name="pipeName">The pipe name.</param>
    /// <returns><see langword="true"/> when the pipe exists.</returns>
    public static bool IsRunning(string pipeName)
    {
        Debug.Assert(!string.IsNullOrWhiteSpace(pipeName), "Precondition: pipeName must not be empty.");

        // Enumerating does not connect, unlike File.Exists, which would consume a pipe instance.
        return Directory.EnumerateFiles(PipeDirectory, pipeName).Any();
    }

    /// <summary>
    /// Lists the pipe names that start with a prefix.
    /// </summary>
    /// <param name="prefix">The prefix, e.g. <c>agent-wpf-ralf-</c>.</param>
    /// <returns>The matching pipe names.</returns>
    public static IReadOnlyList<string> ListPipes(string prefix)
    {
        Debug.Assert(!string.IsNullOrEmpty(prefix), "Precondition: prefix must not be empty.");

        return Directory.EnumerateFiles(PipeDirectory, prefix + "*").Select(p => p[PipeDirectory.Length..]).Order(StringComparer.Ordinal).ToList();
    }

    /// <summary>
    /// Sends one request and returns the daemon's response.
    /// </summary>
    /// <param name="pipeName">The pipe name.</param>
    /// <param name="request">The request.</param>
    /// <param name="connectTimeout">How long to wait for the daemon to accept the connection.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The response.</returns>
    public static async Task<DaemonResponse> SendAsync(string pipeName, DaemonRequest request, TimeSpan connectTimeout, CancellationToken cancellationToken)
    {
        Debug.Assert(request != null, "Precondition: request must not be null.");
        Debug.Assert(connectTimeout > TimeSpan.Zero, "Precondition: connectTimeout must be positive.");

        await using var client = new NamedPipeClientStream(".", pipeName, PipeDirection.InOut, PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);
        await client.ConnectAsync(connectTimeout, cancellationToken);
        await JsonLinesCodec.WriteAsync(client, request, cancellationToken);
        var response = await JsonLinesCodec.ReadResponseAsync(client, cancellationToken)
            ?? throw new IOException("The daemon closed the connection without answering.");

        Debug.Assert(response.ExitCode >= 0, "Postcondition: exit codes are never negative.");
        return response;
    }

    /// <summary>
    /// Starts a detached daemon process for a session. It does not inherit the console handles, so
    /// callers that wait for stdout to close (shells, agents) are not blocked by it.
    /// </summary>
    /// <param name="session">The session name.</param>
    /// <param name="allowedProcesses">Process names the session may attach to.</param>
    public static void StartDaemon(string session, IReadOnlyList<string> allowedProcesses)
    {
        Debug.Assert(!string.IsNullOrWhiteSpace(session), "Precondition: session must not be empty.");
        Debug.Assert(allowedProcesses != null, "Precondition: allowedProcesses must not be null.");

        var start = new ProcessStartInfo(Environment.ProcessPath!)
        {
            UseShellExecute = true,
            WindowStyle = ProcessWindowStyle.Hidden,
        };
        start.ArgumentList.Add("daemon");
        start.ArgumentList.Add("--session");
        start.ArgumentList.Add(session);
        foreach (var name in allowedProcesses)
        {
            start.ArgumentList.Add("--allow-process");
            start.ArgumentList.Add(name);
        }

        using var process = Process.Start(start);
        Debug.Assert(process != null, "Postcondition: the daemon process must start.");
    }
}
