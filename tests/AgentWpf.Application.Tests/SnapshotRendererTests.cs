using System.Collections.Generic;
using AgentWpf.Application.Snapshots;
using AgentWpf.Domain;
using NUnit.Framework;
using Shouldly;

namespace AgentWpf.Application.Tests;

[TestFixture]
public sealed class SnapshotRendererTests
{
    private Dictionary<string, RefId> refs = null!;

    [SetUp]
    public void SetUp() => this.refs = [];

    [Test]
    public void Renders_role_name_ref_and_automation_id()
    {
        var root = Node("w", Role.Window, "Converter", children:
        [
            Node("b", Role.Button, "OK", automationId: "btnOk"),
        ]);

        var text = this.Render(root, new SnapshotOptions());

        text.ShouldBe(
            "- window \"Converter\" [ref=e1]\n" +
            "  - button \"OK\" [ref=e2] [id=btnOk]\n");
    }

    [Test]
    public void Renders_value_and_only_deviating_states()
    {
        var root = Node("w", Role.Window, "W", children:
        [
            Node("t", Role.Edit, "Pfad", value: "C:\\x", states: ElementState.Disabled),
            Node("c", Role.CheckBox, "Splitten", states: ElementState.Checked),
            Node("n", Role.Button, "Normal"),
        ]);

        var text = this.Render(root, new SnapshotOptions());

        text.ShouldContain("  - edit \"Pfad\" [ref=e2] value=\"C:\\\\x\" [disabled]\n");
        text.ShouldContain("  - checkbox \"Splitten\" [ref=e3] [checked]\n");
        text.ShouldContain("  - button \"Normal\" [ref=e4]\n");
    }

    [Test]
    public void Omits_empty_name()
    {
        var root = Node("w", Role.Window, "W", children: [Node("p", Role.Pane, null)]);

        var text = this.Render(root, new SnapshotOptions());

        text.ShouldContain("  - pane [ref=e2]\n");
    }

    [Test]
    public void Escapes_quotes_and_newlines_in_names()
    {
        var root = Node("w", Role.Window, "Say \"hi\"\nnow");

        var text = this.Render(root, new SnapshotOptions());

        text.ShouldBe("- window \"Say \\\"hi\\\"\\nnow\" [ref=e1]\n");
    }

    [Test]
    public void Interactive_only_lifts_actionable_descendants_and_keeps_root()
    {
        var root = Node("w", Role.Window, "W", children:
        [
            Node("p", Role.Pane, null, children:
            [
                Node("l", Role.Text, "Label"),
                Node("b", Role.Button, "OK"),
            ]),
        ]);

        var text = this.Render(root, new SnapshotOptions { InteractiveOnly = true });

        text.ShouldBe(
            "- window \"W\" [ref=e1]\n" +
            "  - button \"OK\" [ref=e2]\n");
    }

    [Test]
    public void Compact_collapses_unnamed_structural_containers()
    {
        var root = Node("w", Role.Window, "W", children:
        [
            Node("p", Role.Pane, null, children:
            [
                Node("g", Role.Group, "Optionen", children: [Node("c", Role.CheckBox, "A")]),
            ]),
        ]);

        var text = this.Render(root, new SnapshotOptions { Compact = true });

        text.ShouldBe(
            "- window \"W\" [ref=e1]\n" +
            "  - group \"Optionen\" [ref=e2]\n" +
            "    - checkbox \"A\" [ref=e3]\n");
    }

    [Test]
    public void Max_depth_cuts_children_and_reports_hidden_count()
    {
        var root = Node("w", Role.Window, "W", children:
        [
            Node("g", Role.Group, "G", children: [Node("a", Role.Button, "A"), Node("b", Role.Button, "B")]),
        ]);

        var text = this.Render(root, new SnapshotOptions { MaxDepth = 1 });

        text.ShouldBe(
            "- window \"W\" [ref=e1]\n" +
            "  - group \"G\" [ref=e2] (+2 children)\n");
    }

    [Test]
    public void Virtualized_container_reports_unrealized_items()
    {
        var root = Node("w", Role.Window, "W", children:
        [
            Node("d", Role.DataGrid, "Orders", totalItemCount: 1000, children:
            [
                Node("r1", Role.DataItem, "Row 1"),
                Node("r2", Role.DataItem, "Row 2"),
            ]),
        ]);

        var text = this.Render(root, new SnapshotOptions());

        text.ShouldContain("    - (998 more items, virtualized; use table or scroll)\n");
    }

    [Test]
    public void Virtualization_hint_counts_only_item_children()
    {
        var root = Node("d", Role.DataGrid, "Orders", totalItemCount: 10, children:
        [
            Node("h", Role.Header, null),
            Node("r1", Role.DataItem, "Row 1"),
            Node("s", Role.ScrollBar, null),
        ]);

        var text = this.Render(root, new SnapshotOptions());

        text.ShouldContain("  - (9 more items, virtualized; use table or scroll)\n");
    }

    [Test]
    public void Verbose_adds_class_name_and_bounds()
    {
        var root = new ElementSnapshot
        {
            RuntimeId = "w",
            Role = Role.Window,
            Name = "W",
            ClassName = "MainWindow",
            Bounds = new Bounds(10, 20, 300, 200),
        };

        var text = this.Render(root, new SnapshotOptions { Verbose = true });

        text.ShouldBe("- window \"W\" [ref=e1] [class=MainWindow] [bounds=10,20,300,200]\n");
    }

    [Test]
    public void Long_values_are_truncated()
    {
        var root = Node("w", Role.Edit, "E", value: new string('x', 300));

        var text = this.Render(root, new SnapshotOptions());

        text.ShouldContain("value=\"" + new string('x', SnapshotRenderer.MaxTextLength) + "…\"");
    }

    private static ElementSnapshot Node(
        string id,
        Role role,
        string? name,
        string? automationId = null,
        string? value = null,
        ElementState states = ElementState.None,
        int? totalItemCount = null,
        ElementSnapshot[]? children = null) => new()
        {
            RuntimeId = id,
            Role = role,
            Name = name,
            AutomationId = automationId,
            Value = value,
            States = states,
            TotalItemCount = totalItemCount,
            Children = children ?? [],
        };

    private string Render(ElementSnapshot root, SnapshotOptions options)
    {
        return SnapshotRenderer.Render(root, this.RefOf, options);
    }

    private RefId RefOf(ElementSnapshot node)
    {
        if (!this.refs.TryGetValue(node.RuntimeId, out var refId))
        {
            refId = new RefId(this.refs.Count + 1);
            this.refs[node.RuntimeId] = refId;
        }

        return refId;
    }
}
