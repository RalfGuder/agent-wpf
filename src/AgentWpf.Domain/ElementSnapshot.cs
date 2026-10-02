using System.Collections.Generic;

namespace AgentWpf.Domain;

/// <summary>
/// An immutable capture of one element of the automation tree and its realized children.
/// </summary>
public sealed class ElementSnapshot
{
    /// <summary>
    /// Gets the UI Automation runtime id in textual form; unique while the element lives.
    /// </summary>
    public required string RuntimeId { get; init; }

    /// <summary>
    /// Gets the semantic role.
    /// </summary>
    public required Role Role { get; init; }

    /// <summary>
    /// Gets the accessible name, if any.
    /// </summary>
    public string? Name { get; init; }

    /// <summary>
    /// Gets the developer-assigned AutomationId, if any.
    /// </summary>
    public string? AutomationId { get; init; }

    /// <summary>
    /// Gets the current value (ValuePattern or RangeValuePattern), if any.
    /// </summary>
    public string? Value { get; init; }

    /// <summary>
    /// Gets the deviating states.
    /// </summary>
    public ElementState States { get; init; }

    /// <summary>
    /// Gets the framework class name, shown with <c>--verbose</c>.
    /// </summary>
    public string? ClassName { get; init; }

    /// <summary>
    /// Gets the bounding rectangle, shown with <c>--verbose</c>.
    /// </summary>
    public Bounds? Bounds { get; init; }

    /// <summary>
    /// Gets the total number of items of a virtualized container, when known; realized
    /// children may be fewer.
    /// </summary>
    public int? TotalItemCount { get; init; }

    /// <summary>
    /// Gets the realized children.
    /// </summary>
    public IReadOnlyList<ElementSnapshot> Children { get; init; } = [];
}
