using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using AgentWpf.Application.Ports;
using AgentWpf.Domain;
using FlaUI.Core;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.Conditions;
using FlaUI.Core.Definitions;
using Debug = System.Diagnostics.Debug;
using UiaToggleState = FlaUI.Core.Definitions.ToggleState;

namespace AgentWpf.Infrastructure.FlaUI;

/// <content>
/// Capturing subtrees with a single cross-process call through a UIA cache request.
/// </content>
public sealed partial class FlaUiAutomationDriver
{
    /// <inheritdoc />
    public ElementSnapshot Capture(string runtimeId, int maxDepth)
    {
        Debug.Assert(!string.IsNullOrEmpty(runtimeId), "Precondition: runtimeId must not be empty.");
        Debug.Assert(maxDepth >= 0, "Precondition: maxDepth must not be negative.");

        var snapshot = this.On(runtimeId, element =>
        {
            var request = this.BuildCacheRequest(maxDepth == 0 ? TreeScope.Element : TreeScope.Subtree);
            using (request.Activate())
            {
                var cached = element.FindFirst(TreeScope.Element, TrueCondition.Default)
                    ?? throw new ElementGoneException(runtimeId);
                return this.Convert(cached, 0, maxDepth);
            }
        });

        Debug.Assert(snapshot.RuntimeId.Length > 0, "Postcondition: a captured element has a runtime id.");
        return snapshot;
    }

    private CacheRequest BuildCacheRequest(TreeScope scope)
    {
        var p = this.automation.PropertyLibrary;
        // Full mode keeps a live reference, so later commands can call patterns not in the cache.
        var request = new CacheRequest { TreeScope = scope, AutomationElementMode = AutomationElementMode.Full };
        foreach (var property in new[]
        {
            p.Element.RuntimeId, p.Element.ControlType, p.Element.Name, p.Element.AutomationId, p.Element.ClassName,
            p.Element.BoundingRectangle, p.Element.IsEnabled, p.Element.IsOffscreen, p.Element.HasKeyboardFocus, p.Element.IsPassword,
            p.PatternAvailability.IsValuePatternAvailable, p.PatternAvailability.IsTogglePatternAvailable,
            p.PatternAvailability.IsExpandCollapsePatternAvailable, p.PatternAvailability.IsSelectionItemPatternAvailable,
            p.PatternAvailability.IsRangeValuePatternAvailable, p.PatternAvailability.IsGridPatternAvailable,
            p.Value.Value, p.Value.IsReadOnly, p.Toggle.ToggleState, p.ExpandCollapse.ExpandCollapseState,
            p.SelectionItem.IsSelected, p.RangeValue.Value, p.Grid.RowCount,
        })
        {
            request.Add(property);
        }

        var patterns = this.automation.PatternLibrary;
        foreach (var pattern in new[] { patterns.ValuePattern, patterns.TogglePattern, patterns.ExpandCollapsePattern, patterns.SelectionItemPattern, patterns.RangeValuePattern, patterns.GridPattern })
        {
            request.Add(pattern);
        }

        return request;
    }

    private ElementSnapshot Convert(AutomationElement element, int depth, int maxDepth)
    {
        var role = MapRole(element.Properties.ControlType.ValueOrDefault);
        var children = new List<ElementSnapshot>();
        if (depth < maxDepth && depth < Application.Snapshots.SnapshotRenderer.MaxTreeDepth)
        {
            foreach (var child in element.CachedChildren)
            {
                children.Add(this.Convert(child, depth + 1, maxDepth));
            }
        }

        var rect = element.Properties.BoundingRectangle.ValueOrDefault;
        return new ElementSnapshot
        {
            RuntimeId = this.Remember(element),
            Role = role,
            Name = element.Properties.Name.ValueOrDefault,
            AutomationId = element.Properties.AutomationId.ValueOrDefault,
            ClassName = element.Properties.ClassName.ValueOrDefault,
            Value = ReadValue(element, role),
            States = ReadStates(element, role),
            Bounds = rect.IsEmpty ? null : new Bounds(rect.X, rect.Y, rect.Width, rect.Height),
            TotalItemCount = role is Role.DataGrid or Role.Table && element.Patterns.Grid.IsSupported ? element.Patterns.Grid.Pattern.RowCount.ValueOrDefault : null,
            Children = children,
        };
    }

    private static string? ReadValue(AutomationElement element, Role role)
    {
        if (element.Patterns.Value.IsSupported)
        {
            return element.Properties.IsPassword.ValueOrDefault ? "***" : element.Patterns.Value.Pattern.Value.ValueOrDefault;
        }

        return element.Patterns.RangeValue.IsSupported && role is not Role.ScrollBar
            ? element.Patterns.RangeValue.Pattern.Value.ValueOrDefault.ToString(CultureInfo.InvariantCulture)
            : null;
    }

    private static ElementState ReadStates(AutomationElement element, Role role)
    {
        var states = ElementState.None;
        states |= element.Properties.IsEnabled.ValueOrDefault ? ElementState.None : ElementState.Disabled;
        states |= element.Properties.IsOffscreen.ValueOrDefault ? ElementState.Offscreen : ElementState.None;
        states |= element.Properties.HasKeyboardFocus.ValueOrDefault ? ElementState.Focused : ElementState.None;
        if (element.Patterns.Toggle.IsSupported)
        {
            var toggle = element.Patterns.Toggle.Pattern.ToggleState.ValueOrDefault;
            states |= toggle == UiaToggleState.On ? ElementState.Checked : toggle == UiaToggleState.Indeterminate ? ElementState.Indeterminate : ElementState.None;
        }

        if (element.Patterns.ExpandCollapse.IsSupported)
        {
            var expand = element.Patterns.ExpandCollapse.Pattern.ExpandCollapseState.ValueOrDefault;
            states |= expand is ExpandCollapseState.Expanded or ExpandCollapseState.PartiallyExpanded ? ElementState.Expanded
                : expand == ExpandCollapseState.Collapsed ? ElementState.Collapsed : ElementState.None;
        }

        if (element.Patterns.SelectionItem.IsSupported && element.Patterns.SelectionItem.Pattern.IsSelected.ValueOrDefault)
        {
            states |= ElementState.Selected;
        }

        if (role == Role.Edit && element.Patterns.Value.IsSupported && element.Patterns.Value.Pattern.IsReadOnly.ValueOrDefault)
        {
            states |= ElementState.ReadOnly;
        }

        return states;
    }

    private static Role MapRole(ControlType type)
        => System.Enum.TryParse<Role>(type.ToString(), ignoreCase: false, out var role) ? role : Role.Unknown;

    private static ToggleStateMapping Map(UiaToggleState state) => new(state);

    // Keeps the mapping of FlaUI toggle states in one place.
    private readonly record struct ToggleStateMapping(UiaToggleState State)
    {
        public Application.Ports.ToggleState ToPort() => this.State switch
        {
            UiaToggleState.On => Application.Ports.ToggleState.On,
            UiaToggleState.Indeterminate => Application.Ports.ToggleState.Indeterminate,
            _ => Application.Ports.ToggleState.Off,
        };
    }
}
