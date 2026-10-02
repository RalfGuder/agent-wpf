using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using AgentWpf.Domain;

namespace AgentWpf.Application.Refs;

/// <summary>
/// Maps session refs (<c>@eN</c>) to UI Automation runtime ids. A runtime id keeps its ref for as
/// long as the session knows it, so refs stay stable across snapshots. Numbers are never reused,
/// which makes a stale ref fail loudly instead of silently hitting a different element.
/// </summary>
public sealed class RefRegistry
{
    /// <summary>
    /// The maximum number of live refs per session; more indicates a runaway snapshot loop.
    /// </summary>
    public const int MaxRefs = 1_000_000;

    private readonly Dictionary<string, RefId> refsByRuntimeId = [];

    private readonly Dictionary<RefId, string> runtimeIdsByRef = [];

    private int lastNumber;

    /// <summary>
    /// Gets the number of live refs.
    /// </summary>
    public int Count => this.runtimeIdsByRef.Count;

    /// <summary>
    /// Returns the ref of a runtime id, assigning the next free number on first sight.
    /// </summary>
    /// <param name="runtimeId">The textual UI Automation runtime id.</param>
    /// <returns>The stable ref of the element.</returns>
    public RefId Assign(string runtimeId)
    {
        Debug.Assert(!string.IsNullOrEmpty(runtimeId), "Precondition: runtimeId must not be empty.");
        Debug.Assert(this.Count < MaxRefs, "Precondition: ref registry exceeds the sane maximum.");

        if (!this.refsByRuntimeId.TryGetValue(runtimeId, out var refId))
        {
            refId = new RefId(++this.lastNumber);
            this.refsByRuntimeId.Add(runtimeId, refId);
            this.runtimeIdsByRef.Add(refId, runtimeId);
        }

        Debug.Assert(this.runtimeIdsByRef[refId] == runtimeId, "Postcondition: both maps must agree.");
        return refId;
    }

    /// <summary>
    /// Returns the runtime id of a ref.
    /// </summary>
    /// <param name="refId">The ref given by the agent.</param>
    /// <returns>The textual runtime id.</returns>
    /// <exception cref="AgentWpfException">The ref is unknown or was forgotten (<see cref="ErrorCode.StaleRef"/>).</exception>
    public string Resolve(RefId refId)
    {
        Debug.Assert(refId.Number > 0, "Precondition: refId must be initialized.");

        if (!this.runtimeIdsByRef.TryGetValue(refId, out var runtimeId))
        {
            throw StaleRef(refId);
        }

        Debug.Assert(this.refsByRuntimeId[runtimeId] == refId, "Postcondition: both maps must agree.");
        return runtimeId;
    }

    /// <summary>
    /// Forgets a ref whose element no longer exists.
    /// </summary>
    /// <param name="refId">The ref to forget.</param>
    public void Forget(RefId refId)
    {
        Debug.Assert(refId.Number > 0, "Precondition: refId must be initialized.");

        if (this.runtimeIdsByRef.Remove(refId, out var runtimeId))
        {
            this.refsByRuntimeId.Remove(runtimeId);
        }

        Debug.Assert(!this.runtimeIdsByRef.ContainsKey(refId), "Postcondition: ref must be gone.");
    }

    /// <summary>
    /// Forgets all refs, e.g. after detaching; numbering continues so old refs stay stale.
    /// </summary>
    public void Clear()
    {
        Debug.Assert(this.refsByRuntimeId.Count == this.runtimeIdsByRef.Count, "Precondition: maps must agree.");

        this.refsByRuntimeId.Clear();
        this.runtimeIdsByRef.Clear();

        Debug.Assert(this.Count == 0, "Postcondition: registry must be empty.");
    }

    /// <summary>
    /// Creates the error reported for an unknown or vanished element.
    /// </summary>
    /// <param name="refId">The stale ref.</param>
    /// <returns>An exception with <see cref="ErrorCode.StaleRef"/> and a re-snapshot hint.</returns>
    public static AgentWpfException StaleRef(RefId refId) => new(
        ErrorCode.StaleRef,
        string.Format(CultureInfo.InvariantCulture, "Element @{0} no longer exists.", refId),
        "run snapshot again");
}
