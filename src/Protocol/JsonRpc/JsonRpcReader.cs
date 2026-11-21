using System.Text;

namespace CSharpSense.Protocol.JsonRpc;

/// <summary>
/// Reads JSON-RPC messages from a stream using Content-Length framing.
/// Each call to <see cref="ReadMessageAsync"/> consumes exactly one message.
/// Returns <c>null</c> on clean EOF.
/// </summary>
public sealed class JsonRpcReader : IDisposable
{
    private readonly Stream _stream;
    private readonly byte[] _singleByte = new byte[1];

    public JsonRpcReader(Stream stream) => _stream = stream;

    public async Task<string?> ReadMessageAsync(CancellationToken ct = default)
    {
        int contentLength = -1;

        while (true)
        {
            string? line = await ReadAsciiLineAsync(ct);
            if (line is null) return null;          // clean EOF
            if (line.Length == 0) break;             // blank line = end of headers

            const string prefix = "Content-Length:";
            if (line.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                ReadOnlySpan<char> valueSpan = line.AsSpan(prefix.Length).Trim();
                if (!int.TryParse(valueSpan, out contentLength) || contentLength < 0)
                    throw new InvalidDataException($"Bad Content-Length header: '{line}'");
            }
            // Other headers (Content-Type) are intentionally ignored.
        }

        if (contentLength < 0)
            throw new InvalidDataException("No Content-Length header found before blank line.");

        byte[] body = new byte[contentLength];
        await _stream.ReadExactlyAsync(body.AsMemory(), ct);
        return Encoding.UTF8.GetString(body);
    }

    // Reads one CRLF-terminated ASCII line from the stream, byte by byte.
    // Returns null on EOF before any character is read.
    private async Task<string?> ReadAsciiLineAsync(CancellationToken ct)
    {
        var sb = new StringBuilder(64);
        bool anyRead = false;

        while (true)
        {
            int n = await _stream.ReadAsync(_singleByte.AsMemory(0, 1), ct);
            if (n == 0)
                return anyRead ? sb.ToString() : null;  // EOF

            anyRead = true;
            char c = (char)_singleByte[0];

            if (c == '\r')
            {
                // Consume the expected '\n'.
                n = await _stream.ReadAsync(_singleByte.AsMemory(0, 1), ct);
                if (n > 0 && _singleByte[0] == '\n')
                    return sb.ToString();

                // Malformed CRLF — treat '\r' as a regular character.
                sb.Append('\r');
                if (n > 0) sb.Append((char)_singleByte[0]);
            }
            else
            {
                sb.Append(c);
            }
        }
    }

    public void Dispose() { /* stream lifetime managed by caller */ }
}
