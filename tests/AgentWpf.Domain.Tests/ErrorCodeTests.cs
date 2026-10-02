using AgentWpf.Domain;
using NUnit.Framework;
using Shouldly;

namespace AgentWpf.Domain.Tests;

[TestFixture]
public sealed class ErrorCodeTests
{
    [TestCase(ErrorCode.None, 0)]
    [TestCase(ErrorCode.ActionFailed, 1)]
    [TestCase(ErrorCode.Usage, 2)]
    [TestCase(ErrorCode.StaleRef, 3)]
    [TestCase(ErrorCode.Timeout, 4)]
    [TestCase(ErrorCode.NotAllowed, 5)]
    [TestCase(ErrorCode.AccessDenied, 6)]
    public void Error_codes_map_to_documented_exit_codes(ErrorCode code, int exitCode)
    {
        ((int)code).ShouldBe(exitCode);
    }

    [TestCase(ErrorCode.StaleRef, "stale-ref")]
    [TestCase(ErrorCode.NotAllowed, "not-allowed")]
    [TestCase(ErrorCode.AccessDenied, "access-denied")]
    [TestCase(ErrorCode.ActionFailed, "action-failed")]
    public void Error_codes_have_stable_kebab_case_names(ErrorCode code, string name)
    {
        code.ToKebabName().ShouldBe(name);
    }

    [Test]
    public void AgentWpfException_carries_code_message_and_hint()
    {
        var ex = new AgentWpfException(ErrorCode.StaleRef, "Ref @e3 is gone.", "run snapshot again");

        ex.Code.ShouldBe(ErrorCode.StaleRef);
        ex.Message.ShouldBe("Ref @e3 is gone.");
        ex.Hint.ShouldBe("run snapshot again");
    }
}
