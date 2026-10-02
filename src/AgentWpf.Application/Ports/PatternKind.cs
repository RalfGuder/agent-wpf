namespace AgentWpf.Application.Ports;

/// <summary>
/// UI Automation control patterns that agent-wpf uses to act on elements without synthetic input.
/// </summary>
public enum PatternKind
{
    /// <summary>InvokePattern: buttons, menu items, hyperlinks.</summary>
    Invoke,

    /// <summary>ValuePattern: text fields and editable combo boxes.</summary>
    Value,

    /// <summary>TogglePattern: check boxes and toggle buttons.</summary>
    Toggle,

    /// <summary>SelectionItemPattern: list items, tab items, radio buttons.</summary>
    SelectionItem,

    /// <summary>ExpandCollapsePattern: combo boxes, tree items, menus.</summary>
    ExpandCollapse,

    /// <summary>ScrollPattern: scrollable containers.</summary>
    Scroll,

    /// <summary>ScrollItemPattern: items that can be scrolled into view.</summary>
    ScrollItem,

    /// <summary>WindowPattern: windows that can be closed.</summary>
    Window,

    /// <summary>GridPattern: data grids and tables.</summary>
    Grid,

    /// <summary>RangeValuePattern: sliders and spinners.</summary>
    RangeValue,
}
