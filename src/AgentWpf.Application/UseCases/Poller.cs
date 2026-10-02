using System;
using System.Diagnostics;
using AgentWpf.Application.Ports;

namespace AgentWpf.Application.UseCases;

/// <summary>
/// Polls a condition until it holds or a timeout elapses.
/// </summary>
public static class Poller
{
    /// <summary>
    /// The interval between two checks.
    /// </summary>
    public static readonly TimeSpan Interval = TimeSpan.FromMilliseconds(100);

    /// <summary>
    /// Checks <paramref name="condition"/> immediately and then every <see cref="Interval"/>.
    /// </summary>
    /// <param name="clock">The clock.</param>
    /// <param name="timeout">The maximum time to wait.</param>
    /// <param name="condition">The condition to satisfy.</param>
    /// <returns><see langword="true"/> when the condition held before the timeout.</returns>
    public static bool Until(IClock clock, TimeSpan timeout, Func<bool> condition)
    {
        Debug.Assert(clock != null && condition != null, "Precondition: clock and condition are required.");
        Debug.Assert(timeout >= TimeSpan.Zero, "Precondition: timeout must not be negative.");

        var start = clock.UtcNow;
        var maxIterations = (int)(timeout.Ticks / Interval.Ticks) + 2;
        for (var i = 0; i < maxIterations; i++)
        {
            if (condition())
            {
                return true;
            }

            var remaining = timeout - (clock.UtcNow - start);
            if (remaining <= TimeSpan.Zero)
            {
                return false;
            }

            clock.Sleep(remaining < Interval ? remaining : Interval);
        }

        Debug.Assert(clock.UtcNow - start >= timeout, "Postcondition: the loop only ends early on success.");
        return false;
    }
}
