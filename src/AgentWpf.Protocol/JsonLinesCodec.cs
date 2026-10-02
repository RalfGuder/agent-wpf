using System;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using System.Threading;
using System.Threading.Tasks;

namespace AgentWpf.Protocol;

/// <summary>
/// Reads and writes protocol messages as single lines of UTF-8 JSON (JSON Lines). Each connection
/// carries exactly one request and one response.
/// </summary>
public static class JsonLinesCodec
{
    /// <summary>
    /// The maximum size of one message in bytes.
    /// </summary>
    public const int MaxMessageBytes = 64 * 1024 * 1024;

    private const int ChunkSize = 64 * 1024;

    /// <summary>
    /// Writes a request followed by a newline.
    /// </summary>
    /// <param name="stream">The target stream.</param>
    /// <param name="request">The request.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task completing when the message is flushed.</returns>
    public static Task WriteAsync(Stream stream, DaemonRequest request, CancellationToken cancellationToken = default)
        => WriteAsync(stream, request, ProtocolJsonContext.Default.DaemonRequest, cancellationToken);

    /// <summary>
    /// Writes a response followed by a newline.
    /// </summary>
    /// <param name="stream">The target stream.</param>
    /// <param name="response">The response.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task completing when the message is flushed.</returns>
    public static Task WriteAsync(Stream stream, DaemonResponse response, CancellationToken cancellationToken = default)
        => WriteAsync(stream, response, ProtocolJsonContext.Default.DaemonResponse, cancellationToken);

    /// <summary>
    /// Reads one request line.
    /// </summary>
    /// <param name="stream">The source stream.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The request, or <see langword="null"/> at the end of the stream.</returns>
    public static Task<DaemonRequest?> ReadRequestAsync(Stream stream, CancellationToken cancellationToken = default)
        => ReadAsync(stream, ProtocolJsonContext.Default.DaemonRequest, cancellationToken);

    /// <summary>
    /// Reads one response line.
    /// </summary>
    /// <param name="stream">The source stream.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The response, or <see langword="null"/> at the end of the stream.</returns>
    public static Task<DaemonResponse?> ReadResponseAsync(Stream stream, CancellationToken cancellationToken = default)
        => ReadAsync(stream, ProtocolJsonContext.Default.DaemonResponse, cancellationToken);

    private static async Task WriteAsync<T>(Stream stream, T message, JsonTypeInfo<T> typeInfo, CancellationToken cancellationToken)
    {
        Debug.Assert(stream is { CanWrite: true }, "Precondition: stream must be writable.");
        Debug.Assert(message != null, "Precondition: message must not be null.");

        var bytes = JsonSerializer.SerializeToUtf8Bytes(message, typeInfo);
        Debug.Assert(Array.IndexOf(bytes, (byte)'\n') < 0, "Invariant: serialized JSON never contains a raw newline.");

        await stream.WriteAsync(bytes, cancellationToken);
        stream.WriteByte((byte)'\n');
        await stream.FlushAsync(cancellationToken);
    }

    private static async Task<T?> ReadAsync<T>(Stream stream, JsonTypeInfo<T> typeInfo, CancellationToken cancellationToken)
        where T : class
    {
        Debug.Assert(stream is { CanRead: true }, "Precondition: stream must be readable.");

        using var line = new MemoryStream();
        var buffer = new byte[ChunkSize];
        for (var total = 0; total < MaxMessageBytes;)
        {
            var read = await stream.ReadAsync(buffer, cancellationToken);
            if (read == 0)
            {
                break;
            }

            var newline = Array.IndexOf(buffer, (byte)'\n', 0, read);
            await line.WriteAsync(buffer.AsMemory(0, newline >= 0 ? newline : read), cancellationToken);
            total += read;
            if (newline >= 0)
            {
                break;
            }
        }

        if (line.Length == 0)
        {
            return null;
        }

        line.Position = 0;
        return await JsonSerializer.DeserializeAsync(line, typeInfo, cancellationToken);
    }
}
