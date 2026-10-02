using System.Diagnostics;
using System.Globalization;
using System.Linq;

namespace AgentWpf.Protocol;

/// <summary>
/// Builds the named pipe names that connect the CLI with a session daemon.
/// </summary>
public static class PipeNames
{
    /// <summary>
    /// Returns the pipe name of a session, e.g. <c>agent-wpf-ralf-default</c>.
    /// </summary>
    /// <param name="userName">The Windows user name.</param>
    /// <param name="session">The session name.</param>
    /// <returns>The pipe name, lower case with unsafe characters replaced by <c>_</c>.</returns>
    public static string ForSession(string userName, string session)
    {
        Debug.Assert(!string.IsNullOrWhiteSpace(userName), "Precondition: userName must not be empty.");
        Debug.Assert(!string.IsNullOrWhiteSpace(session), "Precondition: session must not be empty.");

        var name = "agent-wpf-" + Sanitize(userName) + "-" + Sanitize(session);

        Debug.Assert(name.Length < 256, "Postcondition: pipe names must stay below 256 characters.");
        return name;
    }

    private static string Sanitize(string text) => new(text.Trim()
        .ToLower(CultureInfo.InvariantCulture)
        .Select(c => char.IsAsciiLetterOrDigit(c) || c is '-' or '.' ? c : '_')
        .Take(64)
        .ToArray());
}
