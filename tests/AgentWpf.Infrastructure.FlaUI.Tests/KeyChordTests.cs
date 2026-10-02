using AgentWpf.Domain;
using AgentWpf.Infrastructure.FlaUI;
using FlaUI.Core.WindowsAPI;
using NUnit.Framework;
using Shouldly;

namespace AgentWpf.Infrastructure.FlaUI.Tests;

[TestFixture]
public sealed class KeyChordTests
{
    [Test]
    public void Single_named_key()
    {
        KeyChord.Parse("Enter").ShouldBe([VirtualKeyShort.RETURN]);
    }

    [Test]
    public void Modifier_chord_keeps_order()
    {
        KeyChord.Parse("Control+Shift+S").ShouldBe([VirtualKeyShort.CONTROL, VirtualKeyShort.SHIFT, VirtualKeyShort.KEY_S]);
    }

    [TestCase("ctrl+a", VirtualKeyShort.KEY_A)]
    [TestCase("Alt+F4", VirtualKeyShort.F4)]
    [TestCase("Shift+Tab", VirtualKeyShort.TAB)]
    [TestCase("Ctrl+1", VirtualKeyShort.KEY_1)]
    public void Aliases_letters_digits_and_function_keys(string chord, VirtualKeyShort last)
    {
        KeyChord.Parse(chord)[^1].ShouldBe(last);
    }

    [TestCase("")]
    [TestCase("Hyper")]
    [TestCase("F25")]
    [TestCase("Control+")]
    public void Invalid_chords_are_usage_errors(string chord)
    {
        Should.Throw<AgentWpfException>(() => KeyChord.Parse(chord)).Code.ShouldBe(ErrorCode.Usage);
    }
}
