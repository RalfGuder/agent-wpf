using System;
using System.Collections.Generic;
using AgentWpf.Application.Ports;
using AgentWpf.Application.Sessions;
using AgentWpf.Application.UseCases;
using AgentWpf.Domain;
using Moq;
using NUnit.Framework;
using Shouldly;

namespace AgentWpf.Application.Tests;

[TestFixture]
public sealed class LifecycleUseCasesTests
{
    private Mock<IAutomationDriver> driver = null!;
    private Mock<IProcessLauncher> launcher = null!;
    private FakeClock clock = null!;
    private AgentSession session = null!;
    private LifecycleUseCases sut = null!;

    [SetUp]
    public void SetUp()
    {
        this.driver = new Mock<IAutomationDriver>();
        this.launcher = new Mock<IProcessLauncher>();
        this.clock = new FakeClock();
        this.session = new AgentSession("default");
        this.sut = new LifecycleUseCases(this.session, this.driver.Object, this.launcher.Object, this.clock);
    }

    [Test]
    public void Open_starts_process_trusts_it_and_waits_for_first_window()
    {
        this.launcher.Setup(l => l.Start("app.exe", It.IsAny<IReadOnlyList<string>>(), "C:\\work")).Returns(77);
        this.driver.Setup(d => d.GetProcess(77)).Returns(new ProcessInfo(77, "app", IsElevated: false));
        this.driver.SetupSequence(d => d.GetWindows(77))
            .Returns([])
            .Returns([new WindowInfo("w", "App", false, true)]);

        var result = this.sut.Open("app.exe", [], "C:\\work", TimeSpan.FromSeconds(10));

        this.session.TargetProcessId.ShouldBe(77);
        this.session.LaunchedBySession.ShouldBeTrue();
        this.session.Allowlist.IsAllowed("app", 77).ShouldBeTrue();
        result.Text.ShouldContain("pid 77");
        result.Text.ShouldContain("\"App\"");
    }

    [Test]
    public void Open_times_out_when_no_window_appears()
    {
        this.launcher.Setup(l => l.Start(It.IsAny<string>(), It.IsAny<IReadOnlyList<string>>(), It.IsAny<string>())).Returns(77);
        this.driver.Setup(d => d.GetProcess(77)).Returns(new ProcessInfo(77, "app", false));
        this.driver.Setup(d => d.GetWindows(77)).Returns([]);

        var ex = Should.Throw<AgentWpfException>(() => this.sut.Open("app.exe", [], "C:\\", TimeSpan.FromSeconds(2)));

        ex.Code.ShouldBe(ErrorCode.Timeout);
    }

    [Test]
    public void Attach_by_pid_requires_allowlist()
    {
        this.driver.Setup(d => d.GetProcess(5)).Returns(new ProcessInfo(5, "outlook", false));

        var ex = Should.Throw<AgentWpfException>(() => this.sut.Attach(AttachTarget.ForProcessId(5)));

        ex.Code.ShouldBe(ErrorCode.NotAllowed);
        ex.Hint!.ShouldContain("--allow-process outlook");
        this.session.TargetProcessId.ShouldBeNull();
    }

    [Test]
    public void Attach_by_name_succeeds_when_allowlisted()
    {
        this.session.Allowlist.AllowProcessName("notepad");
        this.launcher.Setup(l => l.FindByName("notepad")).Returns([9]);
        this.driver.Setup(d => d.GetProcess(9)).Returns(new ProcessInfo(9, "notepad", false));

        this.sut.Attach(AttachTarget.ForProcessName("notepad"));

        this.session.TargetProcessId.ShouldBe(9);
        this.session.LaunchedBySession.ShouldBeFalse();
    }

    [Test]
    public void Attach_by_name_with_several_matches_asks_for_pid()
    {
        this.launcher.Setup(l => l.FindByName("notepad")).Returns([9, 10]);

        var ex = Should.Throw<AgentWpfException>(() => this.sut.Attach(AttachTarget.ForProcessName("notepad")));

        ex.Code.ShouldBe(ErrorCode.ActionFailed);
        ex.Message.ShouldContain("9");
        ex.Message.ShouldContain("10");
        ex.Hint!.ShouldContain("--pid");
    }

    [Test]
    public void Attach_by_title_matches_regex_against_desktop_windows()
    {
        this.session.Allowlist.AllowProcessName("calc");
        this.driver.Setup(d => d.GetDesktopWindows()).Returns([new DesktopWindow(3, "Rechner"), new DesktopWindow(4, "Editor")]);
        this.driver.Setup(d => d.GetProcess(3)).Returns(new ProcessInfo(3, "calc", false));

        this.sut.Attach(AttachTarget.ForTitle("^Rech"));

        this.session.TargetProcessId.ShouldBe(3);
    }

    [Test]
    public void Attach_to_elevated_process_reports_access_denied()
    {
        this.session.Allowlist.AllowProcessName("admin");
        this.driver.Setup(d => d.GetProcess(5)).Returns(new ProcessInfo(5, "admin", IsElevated: true));

        var ex = Should.Throw<AgentWpfException>(() => this.sut.Attach(AttachTarget.ForProcessId(5)));

        ex.Code.ShouldBe(ErrorCode.AccessDenied);
    }

    [Test]
    public void Close_of_attached_process_only_detaches()
    {
        this.session.AttachTo(9, launchedBySession: false);

        this.sut.Close(kill: false);

        this.session.TargetProcessId.ShouldBeNull();
        this.driver.Verify(d => d.CloseWindow(It.IsAny<string>()), Times.Never);
        this.launcher.Verify(l => l.Kill(It.IsAny<int>()), Times.Never);
    }

    [Test]
    public void Close_of_launched_process_closes_windows_and_waits()
    {
        this.session.AttachTo(9, launchedBySession: true);
        this.driver.Setup(d => d.GetWindows(9)).Returns([new WindowInfo("w", "App", false, true)]);
        this.launcher.Setup(l => l.WaitForExit(9, It.IsAny<TimeSpan>())).Returns(true);

        this.sut.Close(kill: false);

        this.driver.Verify(d => d.CloseWindow("w"));
        this.session.TargetProcessId.ShouldBeNull();
        this.session.Allowlist.IsAllowed("x", 9).ShouldBeFalse();
    }

    [Test]
    public void Close_reports_process_that_does_not_exit_without_kill()
    {
        this.session.AttachTo(9, launchedBySession: true);
        this.driver.Setup(d => d.GetWindows(9)).Returns([new WindowInfo("w", "App", false, true)]);
        this.launcher.Setup(l => l.WaitForExit(9, It.IsAny<TimeSpan>())).Returns(false);

        var ex = Should.Throw<AgentWpfException>(() => this.sut.Close(kill: false));

        ex.Hint!.ShouldContain("--kill");
        this.session.TargetProcessId.ShouldBe(9);
    }

    [Test]
    public void Close_with_kill_terminates_process_that_does_not_exit()
    {
        this.session.AttachTo(9, launchedBySession: true);
        this.driver.Setup(d => d.GetWindows(9)).Returns([]);
        this.launcher.Setup(l => l.WaitForExit(9, It.IsAny<TimeSpan>())).Returns(false);

        this.sut.Close(kill: true);

        this.launcher.Verify(l => l.Kill(9));
        this.session.TargetProcessId.ShouldBeNull();
    }

    [Test]
    public void Close_with_kill_still_terminates_when_a_window_refuses_to_close()
    {
        this.session.AttachTo(9, launchedBySession: true);
        this.driver.Setup(d => d.GetWindows(9)).Returns([new WindowInfo("p", "", false, false, IsPopup: true), new WindowInfo("w", "App", false, true)]);
        this.driver.Setup(d => d.CloseWindow("p")).Throws(new AgentWpfException(ErrorCode.ActionFailed, "no WindowPattern", null));
        this.launcher.Setup(l => l.WaitForExit(9, It.IsAny<TimeSpan>())).Returns(false);

        this.sut.Close(kill: true);

        this.driver.Verify(d => d.CloseWindow("w"));
        this.launcher.Verify(l => l.Kill(9));
        this.session.TargetProcessId.ShouldBeNull();
    }

    [Test]
    public void Windows_lists_windows_with_refs_and_flags()
    {
        this.session.AttachTo(9, launchedBySession: true);
        this.driver.Setup(d => d.GetWindows(9)).Returns(
        [
            new WindowInfo("d", "Speichern?", IsModal: true, IsForeground: true),
            new WindowInfo("w", "App", IsModal: false, IsForeground: false),
        ]);

        var result = this.sut.Windows();

        result.Text.ShouldBe(
            "- window \"Speichern?\" [ref=e1] [modal] [active]\n" +
            "- window \"App\" [ref=e2]\n");
    }

    [Test]
    public void Switch_window_makes_it_the_active_window()
    {
        this.session.AttachTo(9, launchedBySession: true);
        var refId = this.session.Refs.Assign("w2");
        this.driver.Setup(d => d.GetWindows(9)).Returns([new WindowInfo("w1", "A", false, true), new WindowInfo("w2", "B", false, false)]);

        this.sut.SwitchWindow(refId);

        this.session.ActiveWindowRuntimeId.ShouldBe("w2");
    }
}
