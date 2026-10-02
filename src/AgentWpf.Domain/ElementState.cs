using System;

namespace AgentWpf.Domain;

/// <summary>
/// States of an element that deviate from the normal case. <see cref="None"/> means
/// enabled, unchecked, on screen and editable, so snapshots only print exceptions.
/// </summary>
[Flags]
public enum ElementState
{
    /// <summary>No deviating state.</summary>
    None = 0,

    /// <summary>The element is disabled.</summary>
    Disabled = 1 << 0,

    /// <summary>The element is checked or toggled on.</summary>
    Checked = 1 << 1,

    /// <summary>The element is in the indeterminate toggle state.</summary>
    Indeterminate = 1 << 2,

    /// <summary>The element is expanded.</summary>
    Expanded = 1 << 3,

    /// <summary>The element is collapsed but can be expanded.</summary>
    Collapsed = 1 << 4,

    /// <summary>The element is selected.</summary>
    Selected = 1 << 5,

    /// <summary>The element is scrolled out of view or hidden.</summary>
    Offscreen = 1 << 6,

    /// <summary>The element's value is read-only.</summary>
    ReadOnly = 1 << 7,

    /// <summary>The element has keyboard focus.</summary>
    Focused = 1 << 8,
}
