namespace AgentWpf.Application.Ports;

/// <summary>
/// The state of an element that supports <see cref="PatternKind.Toggle"/>.
/// </summary>
public enum ToggleState
{
    /// <summary>The element is off (unchecked).</summary>
    Off,

    /// <summary>The element is on (checked).</summary>
    On,

    /// <summary>The element is in the third, indeterminate state.</summary>
    Indeterminate,
}
