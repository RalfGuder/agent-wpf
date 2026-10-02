namespace AgentWpf.Domain;

/// <summary>
/// The bounding rectangle of an element in physical screen pixels.
/// </summary>
/// <param name="X">The left edge.</param>
/// <param name="Y">The top edge.</param>
/// <param name="Width">The width.</param>
/// <param name="Height">The height.</param>
public readonly record struct Bounds(int X, int Y, int Width, int Height)
{
    /// <summary>
    /// Gets a value indicating whether the rectangle has no area.
    /// </summary>
    public bool IsEmpty => this.Width <= 0 || this.Height <= 0;
}
