using System;
using System.Diagnostics;
using System.IO;
using NUnit.Framework;
using Shouldly;

namespace AgentWpf.E2E;

[TestFixture]
[Category("E2E")]
[NonParallelizable]
public sealed class TestAppScenarios
{
    private AgentWpfCli cli = null!;

    [SetUp]
    public void SetUp()
    {
        this.cli = new AgentWpfCli();
        this.cli.Ok("open", AgentWpfCli.TestAppExe).Out.ShouldContain("\"Testanwendung\"");
    }

    [TearDown]
    public void TearDown() => this.cli.Dispose();

    [Test]
    public void Snapshot_shows_interactive_elements_with_ids_and_states()
    {
        var snapshot = this.cli.Ok("snapshot", "-i").Out;

        snapshot.ShouldStartWith("- window \"Testanwendung\" [ref=e");
        snapshot.ShouldContain("button \"Begrüßen\"");
        snapshot.ShouldContain("[id=btnGreet]");
        snapshot.ShouldContain("[id=btnDisabled] [disabled]");
        snapshot.ShouldContain("[id=rbSmall] [selected]");
        snapshot.ShouldNotContain("[id=lblResult]");
    }

    [Test]
    public void Fill_and_click_change_the_app_state()
    {
        var snapshot = this.cli.Ok("snapshot", "-i").Out;

        this.cli.Ok("fill", AgentWpfCli.Ref(snapshot, "[id=txtName]"), "Ralf");
        this.cli.Ok("click", AgentWpfCli.Ref(snapshot, "[id=btnGreet]")).Out.ShouldStartWith("invoked @e");

        var full = this.cli.Ok("snapshot").Out;
        this.cli.Ok("get", "text", AgentWpfCli.Ref(full, "[id=lblResult]")).Out.ShouldBe("Hallo, Ralf\n");
    }

    [Test]
    public void Check_select_and_expand_use_patterns()
    {
        var snapshot = this.cli.Ok("snapshot", "-i").Out;

        this.cli.Ok("check", AgentWpfCli.Ref(snapshot, "[id=chkSplit]"));
        this.cli.Ok("select", AgentWpfCli.Ref(snapshot, "[id=cmbColor]"), "Blau");
        this.cli.Ok("check", AgentWpfCli.Ref(snapshot, "[id=rbLarge]"));

        var after = this.cli.Ok("snapshot").Out;
        after.ShouldContain("[id=chkSplit] [checked]");
        after.ShouldContain("[id=rbLarge] [selected]");
        after.ShouldContain("\"Farbe: Blau\"");

        var tree = AgentWpfCli.Ref(after, "\"Projekt\"");
        this.cli.Ok("expand", tree);
        this.cli.Ok("snapshot", "-s", tree).Out.ShouldContain("\"Kalkulation\"");
    }

    [Test]
    public void Virtualized_grid_is_hinted_in_snapshot_and_readable_with_table()
    {
        var snapshot = this.cli.Ok("snapshot").Out;
        snapshot.ShouldContain("more items, virtualized");

        var grid = AgentWpfCli.Ref(snapshot, "[id=gridOrders]");
        var table = this.cli.Ok("table", grid, "--rows", "500-502").Out;

        table.ShouldStartWith("# rows 500-502 of 1000; columns: Nr | Kunde\n");
        table.ShouldContain("[500] 501 | Kunde 501\n");
        table.ShouldContain("[502] 503 | Kunde 503\n");
    }

    [Test]
    public void Modal_dialog_is_snapshotted_first_and_stale_refs_are_reported()
    {
        var snapshot = this.cli.Ok("snapshot", "-i").Out;
        this.cli.Ok("click", AgentWpfCli.Ref(snapshot, "[id=btnDialog]"));
        this.cli.Ok("wait", "--window", "^Bestätigung$").Out.ShouldContain("\"Bestätigung\"");

        var dialog = this.cli.Ok("snapshot", "-i").Out;
        dialog.ShouldStartWith("# 2 windows; showing \"Bestätigung\" (modal).");
        var ok = AgentWpfCli.Ref(dialog, "[id=btnOk]");
        this.cli.Ok("click", ok);

        this.cli.Ok("wait", "--text", "Bestätigt");
        this.cli.Run("click", ok).Code.ShouldBe(3);
    }

    [Test]
    public void Wait_for_delayed_element_and_timeout_exit_code()
    {
        var snapshot = this.cli.Ok("snapshot", "-i").Out;
        this.cli.Ok("click", AgentWpfCli.Ref(snapshot, "[id=btnDelayed]"));

        this.cli.Ok("wait", "--text", "Fertig erschienen", "--timeout", "5000");
        this.cli.Run("wait", "--text", "gibt es nicht", "--timeout", "300").Code.ShouldBe(4);
    }

    [Test]
    public void Second_window_is_listed_and_can_be_activated()
    {
        var snapshot = this.cli.Ok("snapshot", "-i").Out;
        this.cli.Ok("click", AgentWpfCli.Ref(snapshot, "[id=btnSecond]"));
        this.cli.Ok("wait", "--window", "Werkzeuge");

        var windows = this.cli.Ok("windows").Out;
        windows.ShouldContain("\"Werkzeuge\"");
        this.cli.Ok("window", AgentWpfCli.Ref(windows, "\"Werkzeuge\""));
        this.cli.Ok("snapshot", "-i").Out.ShouldContain("[id=btnToolA]");
    }

    [Test]
    public void Menu_items_appear_after_expanding_and_can_be_invoked()
    {
        var snapshot = this.cli.Ok("snapshot", "-i").Out;
        this.cli.Ok("click", AgentWpfCli.Ref(snapshot, "[id=menuFile]"));

        var open = this.cli.Ok("snapshot", "-i").Out;
        this.cli.Ok("click", AgentWpfCli.Ref(open, "[id=menuOpen]"));
        this.cli.Ok("wait", "--text", "Menü Öffnen");
    }

    [Test]
    public void Screenshot_writes_png_of_covered_window()
    {
        var path = Path.Combine(Path.GetTempPath(), "agent-wpf-e2e-" + Guid.NewGuid().ToString("N") + ".png");
        try
        {
            this.cli.Ok("screenshot", path).Out.ShouldStartWith("saved " + path);

            var bytes = File.ReadAllBytes(path);
            bytes[1].ShouldBe((byte)'P');
            bytes.Length.ShouldBeGreaterThan(1000);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Test]
    public void Real_keyboard_input_types_into_focused_field()
    {
        var snapshot = this.cli.Ok("snapshot", "-i").Out;
        var name = AgentWpfCli.Ref(snapshot, "[id=txtName]");

        this.cli.Ok("type", name, "abc");
        this.cli.Ok("press", "Home", "--target", name);
        this.cli.Ok("type", name, "x");

        this.cli.Ok("get", "value", name).Out.ShouldBe("xabc\n");
    }

    [Test]
    public void Json_envelope_and_usage_errors()
    {
        var json = this.cli.Ok("windows", "--json").Out;
        json.ShouldStartWith("{\"ok\":true,\"data\":{\"text\":");

        this.cli.Run("click", "OK").Code.ShouldBe(2);
    }
}

[TestFixture]
[Category("E2E")]
[NonParallelizable]
public sealed class AttachScenarios
{
    [Test]
    public void Attach_requires_allowlist_and_works_when_allowed()
    {
        using var app = Process.Start(AgentWpfCli.TestAppExe)!;
        try
        {
            app.WaitForInputIdle(10_000);
            using (var denied = new AgentWpfCli())
            {
                var result = denied.Run("attach", "--pid", app.Id.ToString(System.Globalization.CultureInfo.InvariantCulture));
                result.Code.ShouldBe(5);
                result.Err.ShouldContain("--allow-process AgentWpf.TestApp");
            }

            using var allowed = new AgentWpfCli("--allow-process", "AgentWpf.TestApp");
            allowed.Ok("attach", "--process", "AgentWpf.TestApp");
            allowed.Ok("snapshot", "-i").Out.ShouldContain("[id=btnGreet]");
            allowed.Ok("close").Out.ShouldStartWith("detached");
            app.HasExited.ShouldBeFalse();
        }
        finally
        {
            app.Kill();
        }
    }
}
