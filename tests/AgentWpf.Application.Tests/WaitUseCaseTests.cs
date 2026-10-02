using System;
using AgentWpf.Application.Ports;
using AgentWpf.Application.UseCases;
using AgentWpf.Domain;
using Moq;
using NUnit.Framework;
using Shouldly;

namespace AgentWpf.Application.Tests;

[TestFixture]
public sealed class WaitUseCaseTests
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(1);

    private Harness h = null!;
    private WaitUseCase sut = null!;

    [SetUp]
    public void SetUp()
    {
        this.h = new Harness();
        this.sut = new WaitUseCase(this.h.Session, this.h.Driver.Object, this.h.Clock);
    }

    [Test]
    public void Fixed_wait_sleeps_once()
    {
        this.sut.Sleep(TimeSpan.FromMilliseconds(250));

        this.h.Clock.Sleeps.ShouldBe([TimeSpan.FromMilliseconds(250)]);
    }

    [Test]
    public void Wait_for_visible_element_returns_when_it_is_on_screen()
    {
        var r = this.h.RefFor("b");
        this.h.Driver.SetupSequence(d => d.Capture("b", 0))
            .Returns(Harness.Node("b", Role.Button, "OK", ElementState.Offscreen))
            .Returns(Harness.Node("b", Role.Button, "OK"));

        this.sut.ForElement(r, ElementWaitState.Visible, Timeout);

        this.h.Clock.Sleeps.Count.ShouldBe(1);
    }

    [Test]
    public void Wait_for_hidden_accepts_vanished_element()
    {
        var r = this.h.RefFor("b");
        this.h.Driver.Setup(d => d.Capture("b", 0)).Throws<ElementGoneException>();

        this.sut.ForElement(r, ElementWaitState.Hidden, Timeout);

        this.h.Clock.Sleeps.ShouldBeEmpty();
    }

    [Test]
    public void Wait_for_enabled_times_out_with_timeout_code()
    {
        var r = this.h.RefFor("b");
        this.h.Driver.Setup(d => d.Capture("b", 0)).Returns(Harness.Node("b", Role.Button, "OK", ElementState.Disabled));

        var ex = Should.Throw<AgentWpfException>(() => this.sut.ForElement(r, ElementWaitState.Enabled, Timeout));

        ex.Code.ShouldBe(ErrorCode.Timeout);
        this.h.Clock.Sleeps.Count.ShouldBeLessThanOrEqualTo(11);
    }

    [Test]
    public void Wait_for_text_searches_names_and_values_of_all_windows()
    {
        this.h.Driver.SetupSequence(d => d.Capture("w1", It.IsAny<int>()))
            .Returns(Harness.Node("w1", Role.Window, "Main"))
            .Returns(Harness.Node("w1", Role.Window, "Main", children: Harness.Node("s", Role.Text, "Fertig: 3 Dateien")));

        this.sut.ForText("fertig", Timeout);

        this.h.Clock.Sleeps.Count.ShouldBe(1);
    }

    [Test]
    public void Wait_for_window_activates_matching_window()
    {
        this.h.Driver.SetupSequence(d => d.GetWindows(100))
            .Returns([new WindowInfo("w1", "Main", false, true)])
            .Returns([new WindowInfo("w1", "Main", false, false), new WindowInfo("d", "Speichern unter", true, true)]);

        var result = this.sut.ForWindow("^Speichern", Timeout);

        this.h.Session.ActiveWindowRuntimeId.ShouldBe("d");
        result.Text.ShouldContain("\"Speichern unter\"");
    }

    [Test]
    public void Wait_for_idle_returns_after_quiet_period()
    {
        var counter = 0L;
        this.h.Driver.Setup(d => d.GetStructureChangeCount(100)).Returns(() => counter);
        this.h.Clock.OnSleep = () => { if (this.h.Clock.Sleeps.Count < 3) { counter++; } };

        this.sut.ForIdle(TimeSpan.FromMilliseconds(300), TimeSpan.FromSeconds(5));

        this.h.Clock.Sleeps.Count.ShouldBeGreaterThanOrEqualTo(5);
    }
}
