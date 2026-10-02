using System;
using System.Collections.Generic;
using System.Text.Json;
using AgentWpf.Application.Ports;
using AgentWpf.Application.Sessions;
using AgentWpf.Cli;
using AgentWpf.Domain;
using Moq;
using NUnit.Framework;
using Shouldly;

namespace AgentWpf.Cli.Tests;

[TestFixture]
public sealed class CommandLineAppTests
{
    private Mock<IAutomationDriver> driver = null!;
    private Mock<IProcessLauncher> launcher = null!;
    private Mock<IScreenCapture> capture = null!;
    private Mock<IClock> clock = null!;
    private AgentSession session = null!;
    private CommandLineApp app = null!;

    [SetUp]
    public void SetUp()
    {
        this.driver = new Mock<IAutomationDriver>();
        this.launcher = new Mock<IProcessLauncher>();
        this.capture = new Mock<IScreenCapture>();
        this.clock = new Mock<IClock>();
        this.clock.Setup(c => c.UtcNow).Returns(DateTimeOffset.UnixEpoch);
        this.session = new AgentSession("default");
        var services = new SessionServices(this.session, this.driver.Object, this.launcher.Object, this.capture.Object, this.clock.Object);
        this.app = new CommandLineApp(() => services);
    }

    [Test]
    public void Error_is_written_to_stderr_with_hint_and_exit_code()
    {
        var response = this.app.Execute(["snapshot", "-i"], "C:\\");

        response.ExitCode.ShouldBe(1);
        response.Stdout.ShouldBeEmpty();
        response.Stderr.ShouldBe("error: No application attached.\nhint: run open <exe> or attach --process <name>\n");
    }

    [Test]
    public void Json_error_uses_envelope_on_stdout()
    {
        var response = this.app.Execute(["snapshot", "--json"], "C:\\");

        response.ExitCode.ShouldBe(1);
        using var doc = JsonDocument.Parse(response.Stdout);
        doc.RootElement.GetProperty("ok").GetBoolean().ShouldBeFalse();
        doc.RootElement.GetProperty("error").GetProperty("code").GetString().ShouldBe("action-failed");
        doc.RootElement.GetProperty("error").GetProperty("hint").GetString().ShouldNotBeNullOrEmpty();
    }

    [Test]
    public void Click_invokes_by_ref()
    {
        this.Attach();
        var refId = this.session.Refs.Assign("b");
        this.driver.Setup(d => d.GetPatterns("b")).Returns(new HashSet<PatternKind> { PatternKind.Invoke });

        var response = this.app.Execute(["click", "@" + refId], "C:\\");

        response.ExitCode.ShouldBe(0);
        response.Stdout.ShouldBe("invoked @e1\n");
        this.driver.Verify(d => d.Invoke("b"));
    }

    [Test]
    public void Invalid_ref_is_a_usage_error()
    {
        var response = this.app.Execute(["click", "OK"], "C:\\");

        response.ExitCode.ShouldBe(2);
        response.Stderr.ShouldContain("@e3");
    }

    [Test]
    public void Unknown_command_is_a_usage_error()
    {
        var response = this.app.Execute(["frobnicate"], "C:\\");

        response.ExitCode.ShouldBe(2);
        response.Stderr.ShouldStartWith("error:");
    }

    [Test]
    public void Stale_ref_returns_exit_code_3()
    {
        this.Attach();

        var response = this.app.Execute(["click", "@e9"], "C:\\");

        response.ExitCode.ShouldBe(3);
        response.Stderr.ShouldContain("hint: run snapshot again");
    }

    [Test]
    public void Table_json_contains_structured_rows()
    {
        this.Attach();
        this.session.Refs.Assign("g");
        this.driver.Setup(d => d.ReadTable("g", 0, 2)).Returns(new TableData(["A"], [new TableRow(0, ["x"]), new TableRow(1, ["y"])], 2));

        var response = this.app.Execute(["table", "@e1", "--rows", "0-1", "--json"], "C:\\");

        response.ExitCode.ShouldBe(0);
        using var doc = JsonDocument.Parse(response.Stdout);
        var data = doc.RootElement.GetProperty("data");
        data.GetProperty("columns")[0].GetString().ShouldBe("A");
        data.GetProperty("rows")[1].GetProperty("cells")[0].GetString().ShouldBe("y");
        data.GetProperty("totalRows").GetInt32().ShouldBe(2);
    }

    [Test]
    public void Text_result_is_wrapped_as_text_in_json()
    {
        this.Attach();
        this.driver.Setup(d => d.GetWindows(100)).Returns([]);

        var response = this.app.Execute(["windows", "--json"], "C:\\");

        using var doc = JsonDocument.Parse(response.Stdout);
        doc.RootElement.GetProperty("ok").GetBoolean().ShouldBeTrue();
        doc.RootElement.GetProperty("data").GetProperty("text").GetString().ShouldBe("no windows\n");
    }

    [Test]
    public void Wait_with_number_sleeps_milliseconds()
    {
        this.Attach();

        var response = this.app.Execute(["wait", "250"], "C:\\");

        response.ExitCode.ShouldBe(0);
        this.clock.Verify(c => c.Sleep(TimeSpan.FromMilliseconds(250)));
    }

    [Test]
    public void Screenshot_resolves_relative_path_against_working_directory()
    {
        this.Attach();
        this.driver.Setup(d => d.GetWindows(100)).Returns([new WindowInfo("w", "W", false, true)]);
        this.driver.Setup(d => d.GetWindowHandle("w")).Returns(7);
        this.capture.Setup(c => c.SavePng(7, null, "C:\\work\\shot.png")).Returns((10, 10));

        var response = this.app.Execute(["screenshot", "shot.png"], "C:\\work");

        response.Stdout.ShouldBe("saved C:\\work\\shot.png (10x10)\n");
    }

    [Test]
    public void Attach_requires_exactly_one_selector()
    {
        var response = this.app.Execute(["attach"], "C:\\");

        response.ExitCode.ShouldBe(2);
        response.Stderr.ShouldContain("--pid");
    }

    [Test]
    public void Skills_get_core_prints_embedded_skill_without_session()
    {
        var local = new CommandLineApp(() => throw new InvalidOperationException("no session needed"));

        var response = local.Execute(["skills", "get", "core"], "C:\\");

        response.ExitCode.ShouldBe(0);
        response.Stdout.ShouldContain("agent-wpf snapshot -i");
    }

    [Test]
    public void Skills_list_names_guides_not_files()
    {
        var local = new CommandLineApp(() => throw new InvalidOperationException("no session needed"));

        var response = local.Execute(["skills", "list"], "C:\\");

        response.Stdout.ShouldBe("core\n");
    }

    [Test]
    public void Skills_get_full_appends_references()
    {
        var local = new CommandLineApp(() => throw new InvalidOperationException("no session needed"));

        var response = local.Execute(["skills", "get", "core", "--full"], "C:\\");

        response.Stdout.ShouldContain("<!-- core/references/commands.md -->");
    }

    [Test]
    public void Help_lists_commands_without_session()
    {
        var local = new CommandLineApp(() => throw new InvalidOperationException("no session needed"));

        var response = local.Execute(["--help"], "C:\\");

        response.ExitCode.ShouldBe(0);
        response.Stdout.ShouldContain("snapshot");
        response.Stdout.ShouldContain("click");
    }

    [Test]
    public void Shutdown_sets_flag()
    {
        var response = this.app.Execute(["shutdown"], "C:\\");

        response.ExitCode.ShouldBe(0);
        this.app.ShutdownRequested.ShouldBeTrue();
    }

    [Test]
    public void Unexpected_exception_becomes_action_failed_and_does_not_escape()
    {
        this.Attach();
        this.driver.Setup(d => d.GetWindows(100)).Throws(new InvalidOperationException("COM boom"));

        var response = this.app.Execute(["windows"], "C:\\");

        response.ExitCode.ShouldBe(1);
        response.Stderr.ShouldContain("COM boom");
    }

    private void Attach()
    {
        this.session.AttachTo(100, launchedBySession: true);
    }
}
