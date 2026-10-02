using System.Collections.Generic;

namespace AgentWpf.Protocol;

/// <summary>
/// A command sent from the CLI to the session daemon: the raw arguments and the caller's working directory.
/// </summary>
/// <param name="Args">The command line arguments without global client options.</param>
/// <param name="WorkingDirectory">The working directory of the CLI, used to resolve relative paths.</param>
public sealed record DaemonRequest(IReadOnlyList<string> Args, string WorkingDirectory)
{
    /// <summary>
    /// The protocol version spoken by this build.
    /// </summary>
    public const int CurrentProtocolVersion = 1;

    /// <summary>
    /// Gets the protocol version of the sender.
    /// </summary>
    public int ProtocolVersion { get; init; } = CurrentProtocolVersion;
}
