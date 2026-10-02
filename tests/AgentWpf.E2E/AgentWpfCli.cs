using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using NUnit.Framework;

namespace AgentWpf.E2E;

/// <summary>Runs the real agent-wpf.exe in an isolated session.</summary>
internal sealed partial class AgentWpfCli : IDisposable
{
    public AgentWpfCli(params string[] globalOptions)
    {
        this.Session = "e2e-" + Guid.NewGuid().ToString("N")[..8];
        this.GlobalOptions = globalOptions;
    }

    public static string RepoRoot { get; } = FindRepoRoot();

    public static string AgentExe { get; } = Path.Combine(RepoRoot, "src", "AgentWpf", "bin", "Debug", "net10.0-windows", "win-x64", "agent-wpf.exe");

    public static string TestAppExe { get; } = Path.Combine(RepoRoot, "tests", "AgentWpf.TestApp", "bin", "Debug", "net10.0-windows", "AgentWpf.TestApp.exe");

    public string Session { get; }

    private string[] GlobalOptions { get; }

    public Result Run(params string[] args)
    {
        var start = new ProcessStartInfo(AgentExe)
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
            WorkingDirectory = Path.GetTempPath(),
        };
        start.ArgumentList.Add("--session");
        start.ArgumentList.Add(this.Session);
        foreach (var option in this.GlobalOptions.Concat(args))
        {
            start.ArgumentList.Add(option);
        }

        using var process = Process.Start(start)!;
        var stdout = process.StandardOutput.ReadToEndAsync();
        var stderr = process.StandardError.ReadToEndAsync();
        if (!process.WaitForExit(TimeSpan.FromSeconds(90)))
        {
            process.Kill();
            throw new TimeoutException("agent-wpf " + string.Join(' ', args) + " hung.");
        }

        var result = new Result(process.ExitCode, stdout.Result, stderr.Result);
        TestContext.Progress.WriteLine($"$ agent-wpf {string.Join(' ', args)} -> {result.Code}\n{result.Out}{result.Err}");
        return result;
    }

    public Result Ok(params string[] args)
    {
        var result = this.Run(args);
        if (result.Code != 0)
        {
            throw new InvalidOperationException($"agent-wpf {string.Join(' ', args)} failed with {result.Code}: {result.Err}");
        }

        return result;
    }

    /// <summary>Finds the ref on the snapshot line containing the marker, e.g. "[id=btnGreet]".</summary>
    public static string Ref(string snapshot, string marker)
    {
        var line = snapshot.Split('\n').FirstOrDefault(l => l.Contains(marker, StringComparison.Ordinal))
            ?? throw new InvalidOperationException("No line with " + marker + " in:\n" + snapshot);
        return "@" + RefPattern().Match(line).Groups[1].Value;
    }

    public void Dispose()
    {
        this.Run("close", "--kill");
        this.Run("session", "stop");
    }

    private static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "AgentWpf.slnx")))
        {
            dir = dir.Parent;
        }

        return dir?.FullName ?? throw new InvalidOperationException("Repository root not found.");
    }

    [GeneratedRegex(@"\[ref=(e\d+)\]")]
    private static partial Regex RefPattern();

    internal sealed record Result(int Code, string Out, string Err);
}
