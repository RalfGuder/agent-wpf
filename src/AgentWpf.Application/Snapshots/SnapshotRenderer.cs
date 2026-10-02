using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Text;
using AgentWpf.Application.UseCases;
using AgentWpf.Domain;

namespace AgentWpf.Application.Snapshots;

/// <summary>
/// Renders an <see cref="ElementSnapshot"/> tree into the compact, token-efficient text format
/// <c>- role "name" [ref=eN] [id=...] value="..." [state]</c>, one element per line.
/// </summary>
public static class SnapshotRenderer
{
    /// <summary>
    /// The maximum number of characters printed for a name or value before it is truncated.
    /// </summary>
    public const int MaxTextLength = TextFormat.MaxTextLength;

    /// <summary>
    /// The maximum tree depth that is traversed; deeper elements are ignored.
    /// </summary>
    public const int MaxTreeDepth = 256;

    /// <summary>
    /// The maximum number of rendered elements before the output is truncated.
    /// </summary>
    public const int MaxRenderedNodes = 5000;

    // State flags in output order with their rendered labels.
    private static readonly (ElementState State, string Label)[] StateLabels =
    [
        (ElementState.Disabled, "disabled"),
        (ElementState.Checked, "checked"),
        (ElementState.Indeterminate, "indeterminate"),
        (ElementState.Expanded, "expanded"),
        (ElementState.Collapsed, "collapsed"),
        (ElementState.Selected, "selected"),
        (ElementState.Offscreen, "offscreen"),
        (ElementState.ReadOnly, "readonly"),
        (ElementState.Focused, "focused"),
    ];

    /// <summary>
    /// Renders the tree below <paramref name="root"/>.
    /// </summary>
    /// <param name="root">The root element; always rendered, even when filters would drop it.</param>
    /// <param name="refOf">Assigns the session ref of every rendered element.</param>
    /// <param name="options">The filter and rendering options.</param>
    /// <returns>The rendered text, terminated by a newline.</returns>
    public static string Render(ElementSnapshot root, Func<ElementSnapshot, RefId> refOf, SnapshotOptions options)
    {
        Debug.Assert(root != null, "Precondition: root must not be null.");
        Debug.Assert(refOf != null, "Precondition: refOf must not be null.");
        Debug.Assert(options != null, "Precondition: options must not be null.");
        Debug.Assert(options.MaxDepth is null or >= 0, "Precondition: MaxDepth must not be negative.");

        var view = Project(root, options, isRoot: true, depth: 0);
        var writer = new Writer(refOf, options);
        foreach (var node in view)
        {
            writer.Write(node, level: 0);
        }

        var text = writer.ToString();

        Debug.Assert(text.EndsWith('\n'), "Postcondition: output must end with a newline.");
        return text;
    }

    private static List<ViewNode> Project(ElementSnapshot node, SnapshotOptions options, bool isRoot, int depth)
    {
        var children = new List<ViewNode>();
        if (depth < MaxTreeDepth)
        {
            foreach (var child in node.Children)
            {
                children.AddRange(Project(child, options, isRoot: false, depth + 1));
            }
        }

        return isRoot || IsKept(node, options) ? [new ViewNode(node, children)] : children;
    }

    private static bool IsKept(ElementSnapshot node, SnapshotOptions options)
    {
        if (node.TotalItemCount.HasValue)
        {
            return true;
        }

        if (options.InteractiveOnly)
        {
            return node.Role.IsInteractive();
        }

        if (options.Compact)
        {
            return !(node.Role.IsStructural() && string.IsNullOrEmpty(node.Name) && string.IsNullOrEmpty(node.AutomationId));
        }

        return true;
    }

    // An element kept by the filters, with its kept (possibly lifted) descendants.
    private sealed record ViewNode(ElementSnapshot Source, List<ViewNode> Children);

    // Accumulates output lines and enforces the depth and node-count limits.
    private sealed class Writer(Func<ElementSnapshot, RefId> refOf, SnapshotOptions options)
    {
        private readonly StringBuilder builder = new();

        private int written;

        private bool truncated;

        public void Write(ViewNode node, int level)
        {
            if (this.written >= MaxRenderedNodes)
            {
                this.AppendTruncationNotice();
                return;
            }

            this.written++;
            var cut = options.MaxDepth is int max && level >= max && node.Children.Count > 0;
            this.AppendLine(node.Source, level, cut ? node.Children.Count : 0);
            if (cut)
            {
                return;
            }

            foreach (var child in node.Children)
            {
                this.Write(child, level + 1);
            }

            this.AppendVirtualizationHint(node.Source, level + 1);
        }

        public override string ToString() => this.builder.ToString();

        private void AppendLine(ElementSnapshot source, int level, int hiddenChildren)
        {
            var b = this.builder;
            b.Append(' ', level * 2).Append("- ").Append(source.Role.ToRoleName());
            if (!string.IsNullOrEmpty(source.Name))
            {
                b.Append(' ').Append(TextFormat.Quote(source.Name));
            }

            b.Append(" [ref=").Append(refOf(source)).Append(']');
            if (!string.IsNullOrEmpty(source.AutomationId))
            {
                b.Append(" [id=").Append(source.AutomationId).Append(']');
            }

            if (source.Value != null)
            {
                b.Append(" value=").Append(TextFormat.Quote(source.Value));
            }

            foreach (var (state, label) in StateLabels)
            {
                if (source.States.HasFlag(state))
                {
                    b.Append(" [").Append(label).Append(']');
                }
            }

            this.AppendVerbose(source);
            if (hiddenChildren > 0)
            {
                b.Append(CultureInfo.InvariantCulture, $" (+{hiddenChildren} children)");
            }

            b.Append('\n');
        }

        private void AppendVerbose(ElementSnapshot source)
        {
            if (!options.Verbose)
            {
                return;
            }

            if (!string.IsNullOrEmpty(source.ClassName))
            {
                this.builder.Append(" [class=").Append(source.ClassName).Append(']');
            }

            if (source.Bounds is Bounds r)
            {
                this.builder.Append(CultureInfo.InvariantCulture, $" [bounds={r.X},{r.Y},{r.Width},{r.Height}]");
            }
        }

        private void AppendVirtualizationHint(ElementSnapshot source, int level)
        {
            if (source.TotalItemCount is not int total)
            {
                return;
            }

            // Headers and scroll bars are children too, but only items count against the total.
            var realized = 0;
            foreach (var child in source.Children)
            {
                realized += child.Role is Role.DataItem or Role.ListItem or Role.TreeItem ? 1 : 0;
            }

            if (total > realized)
            {
                this.builder.Append(' ', level * 2)
                    .Append(CultureInfo.InvariantCulture, $"- ({total - realized} more items, virtualized; use table or scroll)\n");
            }
        }

        private void AppendTruncationNotice()
        {
            if (!this.truncated)
            {
                this.truncated = true;
                this.builder.Append(CultureInfo.InvariantCulture, $"- (truncated after {MaxRenderedNodes} elements; narrow with -s, -d or -i)\n");
            }
        }
    }
}
