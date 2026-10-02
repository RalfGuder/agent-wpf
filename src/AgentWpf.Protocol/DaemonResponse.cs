namespace AgentWpf.Protocol;

/// <summary>
/// The daemon's answer: the exit code and the text the CLI writes to stdout and stderr.
/// </summary>
/// <param name="ExitCode">The process exit code.</param>
/// <param name="Stdout">The standard output.</param>
/// <param name="Stderr">The standard error output.</param>
public sealed record DaemonResponse(int ExitCode, string Stdout, string Stderr);
