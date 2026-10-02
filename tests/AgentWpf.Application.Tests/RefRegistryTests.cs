using AgentWpf.Application.Refs;
using AgentWpf.Domain;
using NUnit.Framework;
using Shouldly;

namespace AgentWpf.Application.Tests;

[TestFixture]
public sealed class RefRegistryTests
{
    [Test]
    public void Same_runtime_id_keeps_its_ref_across_snapshots()
    {
        var registry = new RefRegistry();

        var first = registry.Assign("42.1");
        registry.Assign("42.2");
        var again = registry.Assign("42.1");

        again.ShouldBe(first);
    }

    [Test]
    public void New_runtime_ids_get_increasing_refs_starting_at_e1()
    {
        var registry = new RefRegistry();

        registry.Assign("a").ShouldBe(new RefId(1));
        registry.Assign("b").ShouldBe(new RefId(2));
    }

    [Test]
    public void Resolve_returns_runtime_id_of_known_ref()
    {
        var registry = new RefRegistry();
        var refId = registry.Assign("a");

        registry.Resolve(refId).ShouldBe("a");
    }

    [Test]
    public void Resolve_of_unknown_ref_reports_stale_ref_with_hint()
    {
        var registry = new RefRegistry();

        var ex = Should.Throw<AgentWpfException>(() => registry.Resolve(new RefId(9)));

        ex.Code.ShouldBe(ErrorCode.StaleRef);
        ex.Message.ShouldContain("@e9");
        ex.Hint.ShouldBe("run snapshot again");
    }

    [Test]
    public void Forgotten_ref_becomes_stale_and_is_never_reused()
    {
        var registry = new RefRegistry();
        var refId = registry.Assign("a");

        registry.Forget(refId);

        Should.Throw<AgentWpfException>(() => registry.Resolve(refId)).Code.ShouldBe(ErrorCode.StaleRef);
        registry.Assign("a").ShouldBe(new RefId(2));
    }

    [Test]
    public void Clear_forgets_all_refs_but_keeps_numbering_monotonic()
    {
        var registry = new RefRegistry();
        registry.Assign("a");

        registry.Clear();

        registry.Count.ShouldBe(0);
        registry.Assign("b").ShouldBe(new RefId(2));
    }
}
