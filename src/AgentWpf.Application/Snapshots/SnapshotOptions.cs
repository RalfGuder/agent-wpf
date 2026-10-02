namespace AgentWpf.Application.Snapshots;

/// <summary>
/// Controls how a snapshot is filtered and rendered (<c>-i</c>, <c>-c</c>, <c>-d</c>, <c>--verbose</c>).
/// </summary>
public sealed record SnapshotOptions
{
    /// <summary>
    /// Gets a value indicating whether only actionable elements are rendered (<c>-i</c>).
    /// Non-interactive containers are dropped and their actionable descendants lifted.
    /// </summary>
    public bool InteractiveOnly { get; init; }

    /// <summary>
    /// Gets a value indicating whether unnamed layout containers are collapsed (<c>-c</c>).
    /// </summary>
    public bool Compact { get; init; }

    /// <summary>
    /// Gets the maximum rendered depth below the root (<c>-d</c>); <see langword="null"/> means unlimited.
    /// </summary>
    public int? MaxDepth { get; init; }

    /// <summary>
    /// Gets a value indicating whether class names and bounds are rendered (<c>--verbose</c>).
    /// </summary>
    public bool Verbose { get; init; }
}
