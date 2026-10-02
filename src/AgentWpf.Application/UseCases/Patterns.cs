using System;
using System.Text.RegularExpressions;
using AgentWpf.Domain;

namespace AgentWpf.Application.UseCases;

/// <summary>
/// Builds regular expressions from agent input with a safe timeout.
/// </summary>
public static class Patterns
{
    /// <summary>
    /// Compiles a case-insensitive regular expression or reports a usage error.
    /// </summary>
    /// <param name="pattern">The pattern given by the agent.</param>
    /// <returns>The regular expression.</returns>
    public static Regex Compile(string pattern)
    {
        System.Diagnostics.Debug.Assert(pattern != null, "Precondition: pattern must not be null.");
        try
        {
            return new Regex(pattern, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1));
        }
        catch (ArgumentException ex)
        {
            throw new AgentWpfException(ErrorCode.Usage, "Invalid regular expression: " + ex.Message, "escape special characters such as ( ) [ ] . *");
        }
    }
}
