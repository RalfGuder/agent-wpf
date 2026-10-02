using AgentWpf.Domain;
using NUnit.Framework;
using Shouldly;

namespace AgentWpf.Domain.Tests;

[TestFixture]
public sealed class RefIdTests
{
    [TestCase("@e3", 3)]
    [TestCase("e3", 3)]
    [TestCase("@e120", 120)]
    public void TryParse_accepts_ref_with_and_without_at_sign(string text, int expected)
    {
        RefId.TryParse(text, out var refId).ShouldBeTrue();
        refId.Number.ShouldBe(expected);
    }

    [TestCase("")]
    [TestCase("@")]
    [TestCase("@x3")]
    [TestCase("e")]
    [TestCase("e0")]
    [TestCase("e-1")]
    [TestCase("#btnOk")]
    public void TryParse_rejects_invalid_text(string text)
    {
        RefId.TryParse(text, out _).ShouldBeFalse();
    }

    [Test]
    public void ToString_renders_without_at_sign()
    {
        new RefId(7).ToString().ShouldBe("e7");
    }

    [Test]
    public void Equal_numbers_are_equal_refs()
    {
        new RefId(4).ShouldBe(new RefId(4));
    }
}
