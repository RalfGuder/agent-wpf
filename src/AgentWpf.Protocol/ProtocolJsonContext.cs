using System.Text.Json.Serialization;

namespace AgentWpf.Protocol;

/// <summary>
/// Source-generated JSON metadata for the daemon protocol.
/// </summary>
[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(DaemonRequest))]
[JsonSerializable(typeof(DaemonResponse))]
internal sealed partial class ProtocolJsonContext : JsonSerializerContext
{
}
