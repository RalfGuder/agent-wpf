using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using AgentWpf.Domain;

namespace AgentWpf.Cli;

/// <summary>
/// Serves the agent guides embedded from <c>skill-data/</c>, so the guide always matches the installed version.
/// </summary>
internal static class SkillCatalog
{
    private const string Prefix = "skill-data/";

    /// <summary>
    /// Lists the guide names, e.g. <c>core</c>.
    /// </summary>
    /// <returns>The sorted guide names.</returns>
    public static IReadOnlyList<string> Names()
    {
        var names = Resources().Select(r => r.Path.Split('/')[0]).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToList();

        Debug.Assert(names.Count > 0, "Postcondition: at least the core guide is embedded.");
        return names;
    }

    /// <summary>
    /// Returns a guide's <c>SKILL.md</c>, optionally followed by its reference documents.
    /// </summary>
    /// <param name="name">The guide name.</param>
    /// <param name="full">Whether to append all documents under <c>references/</c>.</param>
    /// <returns>The guide text.</returns>
    public static string Get(string name, bool full)
    {
        Debug.Assert(!string.IsNullOrWhiteSpace(name), "Precondition: name must not be empty.");

        var resources = Resources().Where(r => r.Path.StartsWith(name + "/", StringComparison.Ordinal)).ToList();
        var main = resources.Find(r => r.Path == name + "/SKILL.md");
        if (main.Path is null)
        {
            throw new AgentWpfException(ErrorCode.Usage, "Unknown guide '" + name + "'.", "run agent-wpf skills list");
        }

        var text = new StringBuilder(Read(main.Resource));
        if (full)
        {
            foreach (var reference in resources.Where(r => r.Path.StartsWith(name + "/references/", StringComparison.Ordinal)).OrderBy(r => r.Path, StringComparer.Ordinal))
            {
                text.Append("\n\n<!-- ").Append(reference.Path).Append(" -->\n\n").Append(Read(reference.Resource));
            }
        }

        Debug.Assert(text.Length > 0, "Postcondition: a guide is never empty.");
        return text.ToString().EndsWith('\n') ? text.ToString() : text.Append('\n').ToString();
    }

    private static IEnumerable<(string Path, string Resource)> Resources() => typeof(SkillCatalog).Assembly
        .GetManifestResourceNames()
        .Select(r => (Path: r.Replace('\\', '/'), Resource: r))
        .Where(r => r.Path.StartsWith(Prefix, StringComparison.Ordinal))
        .Select(r => (r.Path[Prefix.Length..], r.Resource));

    private static string Read(string resource)
    {
        using var stream = typeof(SkillCatalog).Assembly.GetManifestResourceStream(resource)!;
        using var reader = new StreamReader(stream, Encoding.UTF8);
        return reader.ReadToEnd().Replace("\r\n", "\n", StringComparison.Ordinal);
    }
}
