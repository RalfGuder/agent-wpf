using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using AgentWpf.Application.Ports;
using AgentWpf.Domain;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.Definitions;
using FlaUI.Core.Input;
using PortMouseButton = AgentWpf.Application.Ports.MouseButton;
using UiaMouseButton = FlaUI.Core.Input.MouseButton;

namespace AgentWpf.Infrastructure.FlaUI;

/// <content>
/// Pattern-based actions and explicit mouse and keyboard input.
/// </content>
public sealed partial class FlaUiAutomationDriver
{
    // Upper bound for walking up to the top-level window.
    private const int MaxAncestorDepth = 128;

    /// <inheritdoc />
    public IReadOnlySet<PatternKind> GetPatterns(string runtimeId) => this.On(runtimeId, e =>
    {
        var p = e.Patterns;
        var set = new HashSet<PatternKind>();
        AddIf(set, PatternKind.Invoke, p.Invoke.IsSupported);
        AddIf(set, PatternKind.Value, p.Value.IsSupported && !p.Value.Pattern.IsReadOnly.ValueOrDefault);
        AddIf(set, PatternKind.Toggle, p.Toggle.IsSupported);
        AddIf(set, PatternKind.SelectionItem, p.SelectionItem.IsSupported);
        AddIf(set, PatternKind.ExpandCollapse, p.ExpandCollapse.IsSupported && p.ExpandCollapse.Pattern.ExpandCollapseState.ValueOrDefault != ExpandCollapseState.LeafNode);
        AddIf(set, PatternKind.Scroll, p.Scroll.IsSupported);
        AddIf(set, PatternKind.ScrollItem, p.ScrollItem.IsSupported);
        AddIf(set, PatternKind.Window, p.Window.IsSupported);
        AddIf(set, PatternKind.Grid, p.Grid.IsSupported);
        AddIf(set, PatternKind.RangeValue, p.RangeValue.IsSupported);
        return set;
    });

    /// <inheritdoc />
    public void Invoke(string runtimeId) => this.On(runtimeId, e => e.Patterns.Invoke.Pattern.Invoke());

    /// <inheritdoc />
    public void SetValue(string runtimeId, string value) => this.On(runtimeId, e =>
    {
        var pattern = e.Patterns.Value.Pattern;
        if (pattern.IsReadOnly.ValueOrDefault)
        {
            throw new AgentWpfException(ErrorCode.ActionFailed, "The field is read-only.", "check the [readonly] state in the snapshot");
        }

        pattern.SetValue(value);
    });

    /// <inheritdoc />
    public Application.Ports.ToggleState GetToggleState(string runtimeId)
        => this.On(runtimeId, e => Map(e.Patterns.Toggle.Pattern.ToggleState.Value).ToPort());

    /// <inheritdoc />
    public void Toggle(string runtimeId) => this.On(runtimeId, e => e.Patterns.Toggle.Pattern.Toggle());

    /// <inheritdoc />
    public void Select(string runtimeId) => this.On(runtimeId, e => e.Patterns.SelectionItem.Pattern.Select());

    /// <inheritdoc />
    public void SelectOption(string runtimeId, string option) => this.On(runtimeId, e =>
    {
        var expand = e.Patterns.ExpandCollapse.PatternOrDefault;
        expand?.Expand();
        var item = FindOption(e, option);
        if (item is null)
        {
            var available = e.FindAllDescendants(cf => cf.ByControlType(ControlType.ListItem)).Select(i => "\"" + i.Name + "\"").Take(20);
            expand?.Collapse();
            throw new AgentWpfException(ErrorCode.ActionFailed, "No option \"" + option + "\". Options: " + string.Join(", ", available), "pick one of the listed options");
        }

        item.Patterns.SelectionItem.Pattern.Select();
        if (expand is not null && expand.ExpandCollapseState.ValueOrDefault == ExpandCollapseState.Expanded)
        {
            expand.Collapse();
        }
    });

    /// <inheritdoc />
    public void Expand(string runtimeId) => this.On(runtimeId, e => e.Patterns.ExpandCollapse.Pattern.Expand());

    /// <inheritdoc />
    public void Collapse(string runtimeId) => this.On(runtimeId, e => e.Patterns.ExpandCollapse.Pattern.Collapse());

    /// <inheritdoc />
    public void Focus(string runtimeId) => this.On(runtimeId, e => e.Focus());

    /// <inheritdoc />
    public void ScrollIntoView(string runtimeId) => this.On(runtimeId, e =>
    {
        if (e.Patterns.VirtualizedItem.IsSupported)
        {
            e.Patterns.VirtualizedItem.Pattern.Realize();
        }

        if (!e.Patterns.ScrollItem.IsSupported)
        {
            throw new AgentWpfException(ErrorCode.ActionFailed, "The element cannot be scrolled into view.", "scroll its container with scroll @container down");
        }

        e.Patterns.ScrollItem.Pattern.ScrollIntoView();
    });

    /// <inheritdoc />
    public void Scroll(string runtimeId, ScrollDirection direction, int pages) => this.On(runtimeId, e =>
    {
        Debug.Assert(pages is > 0 and <= 1000, "Precondition: pages must be between 1 and 1000.");

        var pattern = e.Patterns.Scroll.PatternOrDefault
            ?? throw new AgentWpfException(ErrorCode.ActionFailed, "The element is not scrollable.", "scroll a list, grid or scroll viewer instead");
        var horizontal = direction switch { ScrollDirection.Left => ScrollAmount.LargeDecrement, ScrollDirection.Right => ScrollAmount.LargeIncrement, _ => ScrollAmount.NoAmount };
        var vertical = direction switch { ScrollDirection.Up => ScrollAmount.LargeDecrement, ScrollDirection.Down => ScrollAmount.LargeIncrement, _ => ScrollAmount.NoAmount };
        for (var i = 0; i < pages; i++)
        {
            pattern.Scroll(horizontal, vertical);
        }
    });

    /// <inheritdoc />
    public void Click(string runtimeId, PortMouseButton button, int clickCount) => this.On(runtimeId, e =>
    {
        Debug.Assert(clickCount is 1 or 2, "Precondition: click count must be 1 or 2.");

        TopLevel(e).SetForeground();
        var bounds = e.BoundingRectangle;
        var point = e.TryGetClickablePoint(out var clickable) ? clickable : new System.Drawing.Point(bounds.X + (bounds.Width / 2), bounds.Y + (bounds.Height / 2));
        var uiaButton = button == PortMouseButton.Right ? UiaMouseButton.Right : UiaMouseButton.Left;
        if (clickCount == 2)
        {
            Mouse.DoubleClick(point, uiaButton);
        }
        else
        {
            Mouse.Click(point, uiaButton);
        }

        Wait.UntilInputIsProcessed();
    });

    /// <inheritdoc />
    public void TypeText(string? runtimeId, string text)
    {
        Debug.Assert(text != null, "Precondition: text must not be null.");

        this.FocusForInput(runtimeId);
        Keyboard.Type(text);
        Wait.UntilInputIsProcessed();
    }

    /// <inheritdoc />
    public void PressKeys(string? runtimeId, string keys)
    {
        var chord = KeyChord.Parse(keys);
        this.FocusForInput(runtimeId);
        Keyboard.TypeSimultaneously(chord);
        Wait.UntilInputIsProcessed();
    }

    /// <inheritdoc />
    public void CloseWindow(string runtimeId) => this.On(runtimeId, e => e.Patterns.Window.Pattern.Close());

    private static void AddIf(HashSet<PatternKind> set, PatternKind kind, bool supported)
    {
        if (supported)
        {
            set.Add(kind);
        }
    }

    private static AutomationElement? FindOption(AutomationElement container, string option)
    {
        var items = container.FindAllDescendants(cf => cf.ByControlType(ControlType.ListItem));
        var match = items.FirstOrDefault(i => string.Equals(i.Name, option, StringComparison.OrdinalIgnoreCase))
            ?? items.FirstOrDefault(i => i.FindFirstDescendant(cf => cf.ByName(option)) is not null);
        if (match is not null || !container.Patterns.ItemContainer.IsSupported)
        {
            return match;
        }

        // Virtualized lists only realize visible items; ask the container for the rest.
        var found = container.Patterns.ItemContainer.Pattern.FindItemByProperty(null, container.Automation.PropertyLibrary.Element.Name, option);
        found?.Patterns.VirtualizedItem.PatternOrDefault?.Realize();
        return found;
    }

    private static AutomationElement TopLevel(AutomationElement element)
    {
        var current = element;
        for (var i = 0; i < MaxAncestorDepth; i++)
        {
            var parent = current.Parent;
            if (parent is null || parent.Parent is null)
            {
                return current;
            }

            current = parent;
        }

        return current;
    }

    private void FocusForInput(string? runtimeId)
    {
        if (runtimeId is not null)
        {
            this.On(runtimeId, e =>
            {
                TopLevel(e).SetForeground();
                e.Focus();
            });
        }
    }
}
