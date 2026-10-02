using AgentWpf.Application.Ports;
using AgentWpf.Application.Sessions;
using AgentWpf.Application.Snapshots;
using AgentWpf.Application.UseCases;
using AgentWpf.Domain;
using Moq;
using NUnit.Framework;
using Shouldly;

namespace AgentWpf.Application.Tests;

[TestFixture]
public sealed class SnapshotUseCaseTests
{
    [Test]
    public void Without_target_reports_hint_to_open_or_attach()
    {
        var h = new Harness();
        h.Session.Detach();

        var ex = Should.Throw<AgentWpfException>(() => new SnapshotUseCase(h.Session, h.Driver.Object).Execute(new SnapshotRequest()));

        ex.Hint!.ShouldContain("open");
    }

    [Test]
    public void Renders_active_window_and_assigns_refs()
    {
        var h = new Harness();
        h.Driver.Setup(d => d.Capture("w1", It.IsAny<int>())).Returns(
            Harness.Node("w1", Role.Window, "Main", children: Harness.Node("b", Role.Button, "OK")));

        var result = new SnapshotUseCase(h.Session, h.Driver.Object).Execute(new SnapshotRequest());

        result.Text.ShouldBe("- window \"Main\" [ref=e1]\n  - button \"OK\" [ref=e2]\n");
        h.Session.Refs.Resolve(new RefId(2)).ShouldBe("b");
    }

    [Test]
    public void Prefers_modal_dialog_and_announces_other_windows()
    {
        var h = new Harness();
        h.Driver.Setup(d => d.GetWindows(100)).Returns(
        [
            new WindowInfo("w1", "Main", IsModal: false, IsForeground: false),
            new WindowInfo("d1", "Error", IsModal: true, IsForeground: true),
        ]);
        h.Driver.Setup(d => d.Capture("d1", It.IsAny<int>())).Returns(Harness.Node("d1", Role.Window, "Error"));

        var result = new SnapshotUseCase(h.Session, h.Driver.Object).Execute(new SnapshotRequest());

        result.Text.ShouldStartWith("# 2 windows; showing \"Error\" (modal). Run windows to list them.\n");
        result.Text.ShouldContain("- window \"Error\"");
    }

    [Test]
    public void All_windows_renders_every_window()
    {
        var h = new Harness();
        h.Driver.Setup(d => d.GetWindows(100)).Returns(
        [
            new WindowInfo("w1", "Main", false, true),
            new WindowInfo("w2", "Tool", false, false),
        ]);
        h.Driver.Setup(d => d.Capture("w1", It.IsAny<int>())).Returns(Harness.Node("w1", Role.Window, "Main"));
        h.Driver.Setup(d => d.Capture("w2", It.IsAny<int>())).Returns(Harness.Node("w2", Role.Window, "Tool"));

        var result = new SnapshotUseCase(h.Session, h.Driver.Object).Execute(new SnapshotRequest { AllWindows = true });

        result.Text.ShouldBe("- window \"Main\" [ref=e1]\n- window \"Tool\" [ref=e2]\n");
    }

    [Test]
    public void Scope_ref_renders_subtree()
    {
        var h = new Harness();
        var scope = h.RefFor("g");
        h.Driver.Setup(d => d.Capture("g", It.IsAny<int>())).Returns(Harness.Node("g", Role.Group, "Optionen"));

        var result = new SnapshotUseCase(h.Session, h.Driver.Object).Execute(new SnapshotRequest { Scope = scope });

        result.Text.ShouldBe("- group \"Optionen\" [ref=e1]\n");
    }

    [Test]
    public void Vanished_scope_ref_is_reported_stale()
    {
        var h = new Harness();
        var scope = h.RefFor("g");
        h.Driver.Setup(d => d.Capture("g", It.IsAny<int>())).Throws<ElementGoneException>();

        var ex = Should.Throw<AgentWpfException>(() => new SnapshotUseCase(h.Session, h.Driver.Object).Execute(new SnapshotRequest { Scope = scope }));

        ex.Code.ShouldBe(ErrorCode.StaleRef);
    }

    [Test]
    public void Popups_are_rendered_after_the_active_window_and_never_become_active()
    {
        var h = new Harness();
        h.Driver.Setup(d => d.GetWindows(100)).Returns(
        [
            new WindowInfo("p", "", IsModal: false, IsForeground: true, IsPopup: true),
            new WindowInfo("w1", "Main", IsModal: false, IsForeground: false),
        ]);
        h.Driver.Setup(d => d.Capture("w1", It.IsAny<int>())).Returns(Harness.Node("w1", Role.Window, "Main"));
        h.Driver.Setup(d => d.Capture("p", It.IsAny<int>())).Returns(Harness.Node("p", Role.Menu, null, children: Harness.Node("m", Role.MenuItem, "Öffnen")));

        var result = new SnapshotUseCase(h.Session, h.Driver.Object).Execute(new SnapshotRequest());

        result.Text.ShouldBe(
            "- window \"Main\" [ref=e1]\n" +
            "# popup\n" +
            "- menu [ref=e2]\n" +
            "  - menuitem \"Öffnen\" [ref=e3]\n");
    }

    [Test]
    public void Remembers_explicitly_switched_window_when_no_modal_is_open()
    {
        var h = new Harness();
        h.Driver.Setup(d => d.GetWindows(100)).Returns(
        [
            new WindowInfo("w1", "Main", false, true),
            new WindowInfo("w2", "Tool", false, false),
        ]);
        h.Session.ActiveWindowRuntimeId = "w2";
        h.Driver.Setup(d => d.Capture("w2", It.IsAny<int>())).Returns(Harness.Node("w2", Role.Window, "Tool"));

        var result = new SnapshotUseCase(h.Session, h.Driver.Object).Execute(new SnapshotRequest { Options = new SnapshotOptions { InteractiveOnly = true } });

        result.Text.ShouldContain("- window \"Tool\"");
    }
}
