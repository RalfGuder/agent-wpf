namespace AgentWpf.Application.Ports;

/// <summary>
/// The direction of a <c>scroll</c> command.
/// </summary>
public enum ScrollDirection
{
    /// <summary>Scroll towards the top.</summary>
    Up,

    /// <summary>Scroll towards the bottom.</summary>
    Down,

    /// <summary>Scroll towards the left edge.</summary>
    Left,

    /// <summary>Scroll towards the right edge.</summary>
    Right,
}
