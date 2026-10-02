using System;
using System.Diagnostics;
using System.Globalization;

namespace AgentWpf.Domain;

/// <summary>
/// Identifies an element in a snapshot by a short, session-scoped reference such as <c>e3</c>.
/// Agents address the element with the <c>@e3</c> notation on the command line.
/// </summary>
public readonly record struct RefId
{
    /// <summary>
    /// Initializes a new instance of the <see cref="RefId"/> struct.
    /// </summary>
    /// <param name="number">The positive, session-unique reference number.</param>
    public RefId(int number)
    {
        Debug.Assert(number > 0, "Precondition: ref numbers start at 1.");

        this.Number = number;

        Debug.Assert(this.Number == number, "Postcondition: number must be stored unchanged.");
    }

    /// <summary>
    /// Gets the positive, session-unique reference number.
    /// </summary>
    public int Number { get; }

    /// <summary>
    /// Parses a reference in the forms <c>@e3</c> or <c>e3</c>.
    /// </summary>
    /// <param name="text">The text to parse; may be <see langword="null"/>.</param>
    /// <param name="refId">The parsed reference when the method returns <see langword="true"/>.</param>
    /// <returns><see langword="true"/> when <paramref name="text"/> is a valid reference.</returns>
    public static bool TryParse(string? text, out RefId refId)
    {
        refId = default;
        if (string.IsNullOrEmpty(text))
        {
            return false;
        }

        var span = text.AsSpan();
        if (span[0] == '@')
        {
            span = span[1..];
        }

        if (span.Length < 2 || span[0] != 'e')
        {
            return false;
        }

        var digits = span[1..];
        if (!int.TryParse(digits, NumberStyles.None, CultureInfo.InvariantCulture, out var number) || number <= 0)
        {
            return false;
        }

        refId = new RefId(number);

        Debug.Assert(refId.Number > 0, "Postcondition: a parsed ref is always positive.");
        return true;
    }

    /// <summary>
    /// Returns the reference without the <c>@</c> prefix, e.g. <c>e3</c>.
    /// </summary>
    /// <returns>The textual form used in snapshots.</returns>
    public override string ToString() => "e" + this.Number.ToString(CultureInfo.InvariantCulture);
}
