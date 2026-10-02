using AgentWpf.Application.Ports;
using AgentWpf.Application.UseCases;
using AgentWpf.Domain;
using Moq;
using NUnit.Framework;
using Shouldly;

namespace AgentWpf.Application.Tests;

[TestFixture]
public sealed class InteractionUseCasesTests
{
    private Harness h = null!;
    private InteractionUseCases sut = null!;

    [SetUp]
    public void SetUp()
    {
        this.h = new Harness();
        this.sut = new InteractionUseCases(this.h.Session, this.h.Driver.Object);
    }

    [Test]
    public void Click_prefers_invoke()
    {
        var r = this.h.RefFor("b");
        this.h.Patterns("b", PatternKind.Invoke, PatternKind.Toggle);

        this.sut.Click(r, new ClickOptions());

        this.h.Driver.Verify(d => d.Invoke("b"));
        this.h.Driver.Verify(d => d.Toggle(It.IsAny<string>()), Times.Never);
    }

    [Test]
    public void Click_toggles_checkbox_without_invoke()
    {
        var r = this.h.RefFor("c");
        this.h.Patterns("c", PatternKind.Toggle);

        this.sut.Click(r, new ClickOptions());

        this.h.Driver.Verify(d => d.Toggle("c"));
    }

    [Test]
    public void Click_selects_selection_items()
    {
        var r = this.h.RefFor("t");
        this.h.Patterns("t", PatternKind.SelectionItem);

        this.sut.Click(r, new ClickOptions());

        this.h.Driver.Verify(d => d.Select("t"));
    }

    [Test]
    public void Click_expands_collapsed_expandable()
    {
        var r = this.h.RefFor("m");
        this.h.Patterns("m", PatternKind.ExpandCollapse);
        this.h.Driver.Setup(d => d.Capture("m", 0)).Returns(Harness.Node("m", Role.MenuItem, "Datei", ElementState.Collapsed));

        this.sut.Click(r, new ClickOptions());

        this.h.Driver.Verify(d => d.Expand("m"));
    }

    [Test]
    public void Click_without_pattern_fails_with_input_hint_and_never_moves_the_mouse()
    {
        var r = this.h.RefFor("x");
        this.h.Patterns("x");
        this.h.Driver.Setup(d => d.Capture("x", 0)).Returns(Harness.Node("x", Role.Text, "Label"));

        var ex = Should.Throw<AgentWpfException>(() => this.sut.Click(r, new ClickOptions()));

        ex.Code.ShouldBe(ErrorCode.ActionFailed);
        ex.Message.ShouldContain("@e1");
        ex.Hint!.ShouldContain("--input");
        this.h.Driver.Verify(d => d.Click(It.IsAny<string>(), It.IsAny<MouseButton>(), It.IsAny<int>()), Times.Never);
    }

    [Test]
    public void Click_with_input_uses_real_mouse()
    {
        var r = this.h.RefFor("x");

        this.sut.Click(r, new ClickOptions { Input = true, Button = MouseButton.Right, ClickCount = 2 });

        this.h.Driver.Verify(d => d.Click("x", MouseButton.Right, 2));
    }

    [Test]
    public void Vanished_element_becomes_stale_ref_and_is_forgotten()
    {
        var r = this.h.RefFor("b");
        this.h.Driver.Setup(d => d.GetPatterns("b")).Throws<ElementGoneException>();

        var ex = Should.Throw<AgentWpfException>(() => this.sut.Click(r, new ClickOptions()));

        ex.Code.ShouldBe(ErrorCode.StaleRef);
        this.h.Session.Refs.Count.ShouldBe(0);
    }

    [Test]
    public void Fill_sets_value_via_pattern()
    {
        var r = this.h.RefFor("e");
        this.h.Patterns("e", PatternKind.Value);

        this.sut.Fill(r, "hallo", input: false);

        this.h.Driver.Verify(d => d.SetValue("e", "hallo"));
    }

    [Test]
    public void Fill_without_value_pattern_requires_input()
    {
        var r = this.h.RefFor("e");
        this.h.Patterns("e");
        this.h.Driver.Setup(d => d.Capture("e", 0)).Returns(Harness.Node("e", Role.Custom));

        Should.Throw<AgentWpfException>(() => this.sut.Fill(r, "x", input: false)).Hint!.ShouldContain("--input");
    }

    [Test]
    public void Fill_with_input_focuses_and_types()
    {
        var r = this.h.RefFor("e");

        this.sut.Fill(r, "x", input: true);

        this.h.Driver.Verify(d => d.Focus("e"));
        this.h.Driver.Verify(d => d.PressKeys("e", "Control+A"));
        this.h.Driver.Verify(d => d.TypeText("e", "x"));
    }

    [Test]
    public void Check_toggles_until_on()
    {
        var r = this.h.RefFor("c");
        this.h.Patterns("c", PatternKind.Toggle);
        this.h.Driver.SetupSequence(d => d.GetToggleState("c"))
            .Returns(ToggleState.Off)
            .Returns(ToggleState.Indeterminate)
            .Returns(ToggleState.On);

        this.sut.SetChecked(r, true);

        this.h.Driver.Verify(d => d.Toggle("c"), Times.Exactly(2));
    }

    [Test]
    public void Check_already_on_does_nothing()
    {
        var r = this.h.RefFor("c");
        this.h.Patterns("c", PatternKind.Toggle);
        this.h.Driver.Setup(d => d.GetToggleState("c")).Returns(ToggleState.On);

        this.sut.SetChecked(r, true);

        this.h.Driver.Verify(d => d.Toggle(It.IsAny<string>()), Times.Never);
    }

    [Test]
    public void Check_radio_button_selects_it()
    {
        var r = this.h.RefFor("r");
        this.h.Patterns("r", PatternKind.SelectionItem);

        this.sut.SetChecked(r, true);

        this.h.Driver.Verify(d => d.Select("r"));
    }

    [Test]
    public void Select_delegates_option_text()
    {
        var r = this.h.RefFor("cb");

        this.sut.Select(r, "Gelb");

        this.h.Driver.Verify(d => d.SelectOption("cb", "Gelb"));
    }

    [Test]
    public void Press_without_ref_targets_focused_element()
    {
        this.sut.Press(null, "Enter");

        this.h.Driver.Verify(d => d.PressKeys(null, "Enter"));
    }
}
