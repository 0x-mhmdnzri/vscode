using System.Text;

namespace CSharpSense.Protocol.JsonRpc;

/// <summary>
/// Writes JSON-RPC messages to a stream using Content-Length framing.
/// Thread-safe: concurrent callers are serialised through an internal lock.
/// </summary>
public sealed class JsonRpcWriter : IDisposable
{
    private readonly Stream _stream;
    private readonly SemaphoreSlim _lock = new(1, 1);

    public JsonRpcWriter(Stream stream) => _stream = stream;

    public async Task WriteAsync(string json, CancellationToken ct = default)
    {
        byte[] body   = Encoding.UTF8.GetBytes(json);
        byte[] header = Encoding.ASCII.GetBytes($"Content-Length: {body.Length}\r\n\r\n");

        await _lock.WaitAsync(ct);
        try
        {
            await _stream.WriteAsync(header.AsMemory(), ct);
            await _stream.WriteAsync(body.AsMemory(), ct);
            await _stream.FlushAsync(ct);
        }
        finally
        {
            _lock.Release();
        }
    }

    public void Dispose() => _lock.Dispose();
}
