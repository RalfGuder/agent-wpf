using System.Diagnostics;
using NUnit.Framework;

namespace AgentWpf.E2E;

/// <summary>Kills test apps left over by an aborted run, so that attach-by-name is unambiguous.</summary>
[SetUpFixture]
public sealed class E2ESetUp
{
    [OneTimeSetUp]
    public void KillLeftovers()
    {
        foreach (var process in Process.GetProcessesByName("AgentWpf.TestApp"))
        {
            using (process)
            {
                process.Kill();
                process.WaitForExit(5000);
            }
        }
    }
}
