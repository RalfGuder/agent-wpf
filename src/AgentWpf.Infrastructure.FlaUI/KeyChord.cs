using System;
using System.Collections.Generic;
using System.Diagnostics;
using AgentWpf.Domain;
using FlaUI.Core.WindowsAPI;

namespace AgentWpf.Infrastructure.FlaUI;

/// <summary>
/// Parses key chords such as <c>Enter</c>, <c>Control+S</c> or <c>Alt+F4</c> into virtual keys.
/// </summary>
public static class KeyChord
{
    private const int MaxKeys = 6;

    private static readonly Dictionary<string, VirtualKeyShort> Aliases = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Ctrl"] = VirtualKeyShort.CONTROL,
        ["Control"] = VirtualKeyShort.CONTROL,
        ["Shift"] = VirtualKeyShort.SHIFT,
        ["Alt"] = VirtualKeyShort.ALT,
        ["Win"] = VirtualKeyShort.LWIN,
        ["Meta"] = VirtualKeyShort.LWIN,
        ["Enter"] = VirtualKeyShort.RETURN,
        ["Return"] = VirtualKeyShort.RETURN,
        ["Escape"] = VirtualKeyShort.ESCAPE,
        ["Esc"] = VirtualKeyShort.ESCAPE,
        ["Tab"] = VirtualKeyShort.TAB,
        ["Backspace"] = VirtualKeyShort.BACK,
        ["Delete"] = VirtualKeyShort.DELETE,
        ["Del"] = VirtualKeyShort.DELETE,
        ["Insert"] = VirtualKeyShort.INSERT,
        ["Home"] = VirtualKeyShort.HOME,
        ["End"] = VirtualKeyShort.END,
        ["PageUp"] = VirtualKeyShort.PRIOR,
        ["PageDown"] = VirtualKeyShort.NEXT,
        ["Up"] = VirtualKeyShort.UP,
        ["ArrowUp"] = VirtualKeyShort.UP,
        ["Down"] = VirtualKeyShort.DOWN,
        ["ArrowDown"] = VirtualKeyShort.DOWN,
        ["Left"] = VirtualKeyShort.LEFT,
        ["ArrowLeft"] = VirtualKeyShort.LEFT,
        ["Right"] = VirtualKeyShort.RIGHT,
        ["ArrowRight"] = VirtualKeyShort.RIGHT,
        ["Space"] = VirtualKeyShort.SPACE,
    };

    /// <summary>
    /// Parses a chord.
    /// </summary>
    /// <param name="chord">Keys joined by <c>+</c>, modifiers first.</param>
    /// <returns>The virtual keys in press order.</returns>
    public static VirtualKeyShort[] Parse(string chord)
    {
        Debug.Assert(chord != null, "Precondition: chord must not be null.");

        var parts = chord.Split('+', StringSplitOptions.TrimEntries);
        if (parts.Length is 0 or > MaxKeys)
        {
            throw Invalid(chord);
        }

        var keys = new VirtualKeyShort[parts.Length];
        for (var i = 0; i < parts.Length; i++)
        {
            keys[i] = (parts[i].Length > 0 ? ParseKey(parts[i]) : null) ?? throw Invalid(chord);
        }

        Debug.Assert(keys.Length == parts.Length, "Postcondition: every part maps to a key.");
        return keys;
    }

    private static VirtualKeyShort? ParseKey(string name)
    {
        if (Aliases.TryGetValue(name, out var alias))
        {
            return alias;
        }

        if (name.Length == 1 && char.IsAsciiLetterOrDigit(name[0]))
        {
            return Enum.Parse<VirtualKeyShort>("KEY_" + char.ToUpperInvariant(name[0]));
        }

        return name.Length is 2 or 3 && (name[0] is 'F' or 'f') && int.TryParse(name.AsSpan(1), out var f) && f is >= 1 and <= 24
            ? Enum.Parse<VirtualKeyShort>("F" + f.ToString(System.Globalization.CultureInfo.InvariantCulture))
            : null;
    }

    private static AgentWpfException Invalid(string chord) => new(
        ErrorCode.Usage,
        "'" + chord + "' is not a key chord.",
        "use names like Enter, Escape, Tab, F5, Control+S, Shift+Tab, Alt+F4");
}
