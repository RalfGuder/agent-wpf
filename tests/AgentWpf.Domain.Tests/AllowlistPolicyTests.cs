using AgentWpf.Domain;
using NUnit.Framework;
using Shouldly;

namespace AgentWpf.Domain.Tests;

[TestFixture]
public sealed class AllowlistPolicyTests
{
    [Test]
    public void Empty_policy_allows_nothing()
    {
        var policy = new AllowlistPolicy();

        policy.IsAllowed("notepad", 42).ShouldBeFalse();
    }

    [Test]
    public void Explicitly_trusted_process_id_is_allowed_regardless_of_name()
    {
        var policy = new AllowlistPolicy();

        policy.TrustProcess(42);

        policy.IsAllowed("whatever", 42).ShouldBeTrue();
        policy.IsAllowed("whatever", 43).ShouldBeFalse();
    }

    [TestCase("notepad")]
    [TestCase("Notepad")]
    [TestCase("notepad.exe")]
    [TestCase("NOTEPAD.EXE")]
    public void Allowed_process_name_matches_case_insensitively_with_or_without_extension(string name)
    {
        var policy = new AllowlistPolicy();

        policy.AllowProcessName("notepad.exe");

        policy.IsAllowed(name, 99).ShouldBeTrue();
    }

    [Test]
    public void Other_process_names_stay_denied()
    {
        var policy = new AllowlistPolicy();

        policy.AllowProcessName("notepad");

        policy.IsAllowed("outlook", 1).ShouldBeFalse();
    }

    [Test]
    public void Forgetting_a_process_revokes_trust()
    {
        var policy = new AllowlistPolicy();
        policy.TrustProcess(42);

        policy.ForgetProcess(42);

        policy.IsAllowed("x", 42).ShouldBeFalse();
    }
}
