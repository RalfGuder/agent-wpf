namespace AgentWpf.Application.UseCases;

/// <summary>
/// The outcome of a successful command.
/// </summary>
/// <param name="Text">The human- and agent-readable output, terminated by a newline.</param>
/// <param name="Data">Optional structured data for <c>--json</c>; when absent, the text is used.</param>
public sealed record CommandResult(string Text, object? Data = null);
