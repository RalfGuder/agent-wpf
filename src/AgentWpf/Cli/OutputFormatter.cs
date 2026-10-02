using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using AgentWpf.Application.UseCases;
using AgentWpf.Domain;
using AgentWpf.Protocol;

namespace AgentWpf.Cli;

/// <summary>
/// Turns command results and errors into stdout/stderr text or the JSON envelope
/// <c>{ ok, data, error: { code, message, hint } }</c>.
/// </summary>
internal static class OutputFormatter
{
    private static readonly JsonSerializerOptions DataOptions = new(JsonSerializerDefaults.Web);

    /// <summary>
    /// Formats a successful result.
    /// </summary>
    /// <param name="result">The result.</param>
    /// <param name="json">Whether to emit the JSON envelope.</param>
    /// <returns>A response with exit code 0.</returns>
    public static DaemonResponse Success(CommandResult result, bool json)
    {
        Debug.Assert(result != null, "Precondition: result must not be null.");
        Debug.Assert(result.Text.Length == 0 || result.Text.EndsWith('\n'), "Precondition: text output ends with a newline.");

        if (!json)
        {
            return new DaemonResponse(0, result.Text, string.Empty);
        }

        var data = result.Data is null or string
            ? new JsonObject { ["text"] = result.Text }
            : JsonSerializer.SerializeToNode(result.Data, result.Data.GetType(), DataOptions);
        return new DaemonResponse(0, Envelope(true, data, null), string.Empty);
    }

    /// <summary>
    /// Formats an error.
    /// </summary>
    /// <param name="error">The error.</param>
    /// <param name="json">Whether to emit the JSON envelope.</param>
    /// <returns>A response with the error's exit code.</returns>
    public static DaemonResponse Failure(AgentWpfException error, bool json)
    {
        Debug.Assert(error != null, "Precondition: error must not be null.");
        Debug.Assert(error.Code != ErrorCode.None, "Precondition: an error must carry a failure code.");

        var code = (int)error.Code;
        if (json)
        {
            var node = new JsonObject
            {
                ["code"] = error.Code.ToKebabName(),
                ["message"] = error.Message,
                ["hint"] = error.Hint,
            };
            return new DaemonResponse(code, Envelope(false, null, node), string.Empty);
        }

        var text = new StringBuilder("error: ").Append(error.Message).Append('\n');
        if (!string.IsNullOrEmpty(error.Hint))
        {
            text.Append("hint: ").Append(error.Hint).Append('\n');
        }

        return new DaemonResponse(code, string.Empty, text.ToString());
    }

    private static string Envelope(bool ok, JsonNode? data, JsonNode? error)
        => new JsonObject { ["ok"] = ok, ["data"] = data, ["error"] = error }.ToJsonString() + "\n";
}
