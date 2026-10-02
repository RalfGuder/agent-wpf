using System;

namespace AgentWpf.Application.Ports;

/// <summary>
/// Abstracts time so that waits and timeouts are testable.
/// </summary>
public interface IClock
{
    /// <summary>
    /// Gets the current UTC time.
    /// </summary>
    DateTimeOffset UtcNow { get; }

    /// <summary>
    /// Blocks the calling thread for the given duration.
    /// </summary>
    /// <param name="duration">The non-negative duration.</param>
    void Sleep(TimeSpan duration);
}
