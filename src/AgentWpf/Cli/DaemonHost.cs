using System;
using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Threading;
using System.Threading.Tasks;
using AgentWpf.Protocol;

namespace AgentWpf.Cli;

/// <summary>
/// Serves one session on a named pipe that only the current user can open. Requests are handled one
/// at a time; the daemon ends after an idle period, on cancellation or when a handler asks to stop.
/// </summary>
internal sealed class DaemonHost
{
    private readonly string pipeName;

    private readonly Func<DaemonRequest, DaemonResponse> handler;

    private readonly TimeSpan idleTimeout;

    private readonly Func<bool> stopRequested;

    /// <summary>
    /// Initializes a new instance of the <see cref="DaemonHost"/> class.
    /// </summary>
    /// <param name="pipeName">The pipe name from <see cref="PipeNames.ForSession"/>.</param>
    /// <param name="handler">Executes a request.</param>
    /// <param name="idleTimeout">How long to wait for the next request before exiting.</param>
    /// <param name="stopRequested">Checked after every request; <see langword="true"/> ends the daemon.</param>
    public DaemonHost(string pipeName, Func<DaemonRequest, DaemonResponse> handler, TimeSpan idleTimeout, Func<bool> stopRequested)
    {
        Debug.Assert(!string.IsNullOrWhiteSpace(pipeName), "Precondition: pipeName must not be empty.");
        Debug.Assert(idleTimeout > TimeSpan.Zero, "Precondition: idleTimeout must be positive.");

        this.pipeName = pipeName;
        this.handler = handler;
        this.idleTimeout = idleTimeout;
        this.stopRequested = stopRequested;
    }

    /// <summary>
    /// Serves requests until idle timeout, cancellation or a stop request.
    /// </summary>
    /// <param name="cancellationToken">Cancels the daemon.</param>
    /// <returns>A task that completes when the daemon stops.</returns>
    /// <exception cref="IOException">Another daemon already serves this session.</exception>
    public async Task RunAsync(CancellationToken cancellationToken)
    {
        // FirstPipeInstance makes a second daemon for the same session fail instead of splitting requests.
        var listening = this.CreateServer(PipeOptions.FirstPipeInstance);

        // Not statically bounded by design: a daemon runs until idle, cancelled or asked to stop.
        while (true)
        {
            using var current = listening;
            using var idle = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            idle.CancelAfter(this.idleTimeout);
            try
            {
                await current.WaitForConnectionAsync(idle.Token);
            }
            catch (OperationCanceledException)
            {
                return;
            }

            // Keep one instance listening while this request runs so the pipe never disappears.
            listening = this.CreateServer(PipeOptions.None);
            await this.HandleAsync(current, cancellationToken);
            if (this.stopRequested())
            {
                await listening.DisposeAsync();
                return;
            }
        }
    }

    private NamedPipeServerStream CreateServer(PipeOptions extra) => new(
        this.pipeName,
        PipeDirection.InOut,
        2,
        PipeTransmissionMode.Byte,
        PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly | extra);

    private async Task HandleAsync(NamedPipeServerStream pipe, CancellationToken cancellationToken)
    {
        try
        {
            var request = await JsonLinesCodec.ReadRequestAsync(pipe, cancellationToken);
            if (request is null)
            {
                return;
            }

            var response = request.ProtocolVersion == DaemonRequest.CurrentProtocolVersion
                ? this.Execute(request)
                : new DaemonResponse(2, string.Empty, "error: The running daemon speaks another protocol version.\nhint: run agent-wpf session stop\n");
            await JsonLinesCodec.WriteAsync(pipe, response, cancellationToken);
            pipe.WaitForPipeDrain();
        }
        catch (IOException)
        {
            // The client went away; the next request gets a fresh connection.
        }
    }

    private DaemonResponse Execute(DaemonRequest request)
    {
        try
        {
            return this.handler(request);
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            return new DaemonResponse(1, string.Empty, "error: internal daemon error: " + ex.Message + "\n");
        }
    }
}
