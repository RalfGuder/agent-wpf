using System;
using System.Threading;
using AgentWpf.Application.Ports;

namespace AgentWpf.Infrastructure.FlaUI;

/// <summary>
/// The real clock.
/// </summary>
public sealed class SystemClock : IClock
{
    /// <inheritdoc />
    public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;

    /// <inheritdoc />
    public void Sleep(TimeSpan duration) => Thread.Sleep(duration);
}
