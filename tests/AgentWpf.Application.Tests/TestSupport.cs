using System;
using System.Collections.Generic;
using AgentWpf.Application.Ports;
using AgentWpf.Application.Sessions;
using AgentWpf.Domain;
using Moq;

namespace AgentWpf.Application.Tests;

internal sealed class FakeClock : IClock
{
    public DateTimeOffset UtcNow { get; private set; } = new(2026, 10, 2, 12, 0, 0, TimeSpan.Zero);

    public List<TimeSpan> Sleeps { get; } = [];

    public Action? OnSleep { get; set; }

    public void Sleep(TimeSpan duration)
    {
        this.Sleeps.Add(duration);
        this.UtcNow += duration;
        this.OnSleep?.Invoke();
    }
}

/// <summary>A session attached to pid 100 with one main window "w1".</summary>
internal sealed class Harness
{
    public Harness()
    {
        this.Session = new AgentSession("default");
        this.Session.AttachTo(100, launchedBySession: true);
        this.Driver.Setup(d => d.GetWindows(100)).Returns([new WindowInfo("w1", "Main", IsModal: false, IsForeground: true)]);
    }

    public Mock<IAutomationDriver> Driver { get; } = new(MockBehavior.Loose);

    public Mock<IProcessLauncher> Launcher { get; } = new(MockBehavior.Loose);

    public Mock<IScreenCapture> Capture { get; } = new(MockBehavior.Loose);

    public FakeClock Clock { get; } = new();

    public AgentSession Session { get; }

    public RefId RefFor(string runtimeId) => this.Session.Refs.Assign(runtimeId);

    public void Patterns(string runtimeId, params PatternKind[] patterns)
        => this.Driver.Setup(d => d.GetPatterns(runtimeId)).Returns(new HashSet<PatternKind>(patterns));

    public static ElementSnapshot Node(string id, Role role, string? name = null, ElementState states = ElementState.None, string? value = null, params ElementSnapshot[] children)
        => new() { RuntimeId = id, Role = role, Name = name, States = states, Value = value, Children = children };
}
