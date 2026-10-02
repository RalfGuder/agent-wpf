using System.Diagnostics;
using AgentWpf.Application.Refs;
using AgentWpf.Domain;

namespace AgentWpf.Application.Sessions;

/// <summary>
/// The state of one named agent-wpf session: its target process, refs, allowlist and active window.
/// A session lives in the daemon and survives between CLI invocations.
/// </summary>
public sealed class AgentSession
{
    /// <summary>
    /// Initializes a new instance of the <see cref="AgentSession"/> class.
    /// </summary>
    /// <param name="name">The session name, e.g. <c>default</c>.</param>
    public AgentSession(string name)
    {
        Debug.Assert(!string.IsNullOrWhiteSpace(name), "Precondition: session name must not be empty.");

        this.Name = name;

        Debug.Assert(this.TargetProcessId is null, "Postcondition: a new session has no target.");
    }

    /// <summary>
    /// Gets the session name.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// Gets the policy deciding which processes may be automated.
    /// </summary>
    public AllowlistPolicy Allowlist { get; } = new();

    /// <summary>
    /// Gets the ref registry of this session.
    /// </summary>
    public RefRegistry Refs { get; } = new();

    /// <summary>
    /// Gets the id of the automated process, or <see langword="null"/> when nothing is attached.
    /// </summary>
    public int? TargetProcessId { get; private set; }

    /// <summary>
    /// Gets a value indicating whether the session started the target process itself.
    /// </summary>
    public bool LaunchedBySession { get; private set; }

    /// <summary>
    /// Gets or sets the runtime id of the window chosen with <c>window</c> or <c>wait --window</c>.
    /// </summary>
    public string? ActiveWindowRuntimeId { get; set; }

    /// <summary>
    /// Makes a process the session target, dropping all refs of a previous target.
    /// </summary>
    /// <param name="processId">The process id.</param>
    /// <param name="launchedBySession">Whether the session started the process; such processes are trusted.</param>
    public void AttachTo(int processId, bool launchedBySession)
    {
        Debug.Assert(processId > 0, "Precondition: processId must be positive.");

        this.Detach();
        if (launchedBySession)
        {
            this.Allowlist.TrustProcess(processId);
        }

        this.TargetProcessId = processId;
        this.LaunchedBySession = launchedBySession;

        Debug.Assert(this.Refs.Count == 0, "Postcondition: refs of a previous target must be gone.");
    }

    /// <summary>
    /// Releases the target process without terminating it.
    /// </summary>
    public void Detach()
    {
        if (this.TargetProcessId is int pid && this.LaunchedBySession)
        {
            this.Allowlist.ForgetProcess(pid);
        }

        this.TargetProcessId = null;
        this.LaunchedBySession = false;
        this.ActiveWindowRuntimeId = null;
        this.Refs.Clear();

        Debug.Assert(this.Refs.Count == 0, "Postcondition: refs must be gone.");
    }
}
