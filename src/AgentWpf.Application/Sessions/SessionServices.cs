using System.Diagnostics;
using AgentWpf.Application.Ports;
using AgentWpf.Application.UseCases;

namespace AgentWpf.Application.Sessions;

/// <summary>
/// Bundles a session with all use cases that operate on it. Created once per daemon.
/// </summary>
public sealed class SessionServices
{
    /// <summary>
    /// Initializes a new instance of the <see cref="SessionServices"/> class.
    /// </summary>
    /// <param name="session">The session.</param>
    /// <param name="driver">The automation driver.</param>
    /// <param name="launcher">The process launcher.</param>
    /// <param name="capture">The screen capture.</param>
    /// <param name="clock">The clock.</param>
    public SessionServices(AgentSession session, IAutomationDriver driver, IProcessLauncher launcher, IScreenCapture capture, IClock clock)
    {
        Debug.Assert(session != null && driver != null, "Precondition: session and driver are required.");
        Debug.Assert(launcher != null && capture != null && clock != null, "Precondition: launcher, capture and clock are required.");

        this.Session = session;
        this.Lifecycle = new LifecycleUseCases(session, driver, launcher, clock);
        this.Snapshot = new SnapshotUseCase(session, driver);
        this.Interaction = new InteractionUseCases(session, driver);
        this.Wait = new WaitUseCase(session, driver, clock);
        this.Query = new QueryUseCases(session, driver);
        this.Screenshot = new ScreenshotUseCase(session, driver, capture);
    }

    /// <summary>Gets the session.</summary>
    public AgentSession Session { get; }

    /// <summary>Gets the lifecycle use cases.</summary>
    public LifecycleUseCases Lifecycle { get; }

    /// <summary>Gets the snapshot use case.</summary>
    public SnapshotUseCase Snapshot { get; }

    /// <summary>Gets the interaction use cases.</summary>
    public InteractionUseCases Interaction { get; }

    /// <summary>Gets the wait use case.</summary>
    public WaitUseCase Wait { get; }

    /// <summary>Gets the query use cases.</summary>
    public QueryUseCases Query { get; }

    /// <summary>Gets the screenshot use case.</summary>
    public ScreenshotUseCase Screenshot { get; }
}
