using System;
using System.Threading;
using System.Threading.Tasks;
using AgentWpf.Cli;
using AgentWpf.Protocol;
using NUnit.Framework;
using Shouldly;

namespace AgentWpf.Cli.Tests;

[TestFixture]
public sealed class DaemonPipeTests
{
    [Test]
    public async Task Client_round_trips_request_through_daemon_pipe()
    {
        var pipe = "agent-wpf-test-" + Guid.NewGuid().ToString("N");
        var host = new DaemonHost(pipe, r => new DaemonResponse(0, string.Join(' ', r.Args) + "@" + r.WorkingDirectory, string.Empty), TimeSpan.FromSeconds(30), () => false);
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        var run = host.RunAsync(cts.Token);

        var response = await DaemonClient.SendAsync(pipe, new DaemonRequest(["click", "@e1"], "C:\\w"), TimeSpan.FromSeconds(10), cts.Token);

        response.ShouldBe(new DaemonResponse(0, "click @e1@C:\\w", string.Empty));
        await cts.CancelAsync();
        await run;
    }

    [Test]
    public async Task Daemon_stops_after_idle_timeout()
    {
        var pipe = "agent-wpf-test-" + Guid.NewGuid().ToString("N");
        var host = new DaemonHost(pipe, r => new DaemonResponse(0, string.Empty, string.Empty), TimeSpan.FromMilliseconds(200), () => false);

        var run = host.RunAsync(CancellationToken.None);

        (await Task.WhenAny(run, Task.Delay(TimeSpan.FromSeconds(10)))).ShouldBe(run);
    }

    [Test]
    public async Task Daemon_stops_after_shutdown_request()
    {
        var pipe = "agent-wpf-test-" + Guid.NewGuid().ToString("N");
        var stop = false;
        var host = new DaemonHost(pipe, r => { stop = true; return new DaemonResponse(0, "bye", string.Empty); }, TimeSpan.FromSeconds(30), () => stop);
        var run = host.RunAsync(CancellationToken.None);

        await DaemonClient.SendAsync(pipe, new DaemonRequest(["shutdown"], "C:\\"), TimeSpan.FromSeconds(10), CancellationToken.None);

        (await Task.WhenAny(run, Task.Delay(TimeSpan.FromSeconds(10)))).ShouldBe(run);
    }

    [Test]
    public void Pipe_existence_check_reflects_running_daemon()
    {
        DaemonClient.IsRunning("agent-wpf-test-" + Guid.NewGuid().ToString("N")).ShouldBeFalse();
    }
}
