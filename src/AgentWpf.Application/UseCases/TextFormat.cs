using System;

namespace AgentWpf.Application.UseCases;

/// <summary>
/// Formatting helpers shared by all text outputs.
/// </summary>
public static class TextFormat
{
    /// <summary>
    /// The maximum number of characters printed for a name or value before it is truncated.
    /// </summary>
    public const int MaxTextLength = 120;

    /// <summary>
    /// Truncates, escapes and quotes a text so that it stays on one line.
    /// </summary>
    /// <param name="text">The raw text.</param>
    /// <returns>The quoted text, e.g. <c>"Say \"hi\""</c>.</returns>
    public static string Quote(string text)
    {
        System.Diagnostics.Debug.Assert(text != null, "Precondition: text must not be null.");

        var truncated = text.Length > MaxTextLength ? text[..MaxTextLength] + "…" : text;
        var quoted = "\"" + Escape(truncated) + "\"";

        System.Diagnostics.Debug.Assert(!quoted.Contains('\n', StringComparison.Ordinal), "Postcondition: quoted text is single-line.");
        return quoted;
    }

    /// <summary>
    /// Escapes backslashes, quotes and control characters.
    /// </summary>
    /// <param name="text">The raw text.</param>
    /// <returns>The escaped text.</returns>
    public static string Escape(string text) => text
        .Replace("\\", "\\\\", StringComparison.Ordinal)
        .Replace("\"", "\\\"", StringComparison.Ordinal)
        .Replace("\r", "\\r", StringComparison.Ordinal)
        .Replace("\n", "\\n", StringComparison.Ordinal)
        .Replace("\t", "\\t", StringComparison.Ordinal);
}
