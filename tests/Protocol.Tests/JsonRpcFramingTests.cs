using System.Text;
using CSharpSense.Protocol.JsonRpc;

namespace CSharpSense.Protocol.Tests;

public sealed class JsonRpcFramingTests
{
    // ── Writer helpers ───────────────────────────────────────────────────────

    private static async Task<byte[]> WriteToBytes(params string[] messages)
    {
        using var ms = new MemoryStream();
        using var writer = new JsonRpcWriter(ms);
        foreach (var msg in messages) await writer.WriteAsync(msg);
        return ms.ToArray();
    }

    private static JsonRpcReader ReaderFor(byte[] data) =>
        new JsonRpcReader(new MemoryStream(data));

    // ── Round-trip ───────────────────────────────────────────────────────────

    [Fact]
    public async Task WriteAndRead_SingleMessage_RoundTrip()
    {
        const string json = "{\"test\":\"value\"}";
        var bytes = await WriteToBytes(json);
        string? result = await ReaderFor(bytes).ReadMessageAsync();
        Assert.Equal(json, result);
    }

    [Theory]
    [InlineData("{\"a\":1}")]
    [InlineData("{}")]
    [InlineData("{\"nested\":{\"x\":true}}")]
    public async Task WriteAndRead_VariousJson(string json)
    {
        var bytes = await WriteToBytes(json);
        string? result = await ReaderFor(bytes).ReadMessageAsync();
        Assert.Equal(json, result);
    }

    [Fact]
    public async Task WriteAndRead_MultipleMessages_InOrder()
    {
        string[] messages = ["{\"a\":1}", "{\"b\":2}", "{\"c\":3}"];
        var bytes = await WriteToBytes(messages);

        var reader = ReaderFor(bytes);
        foreach (var expected in messages)
        {
            string? result = await reader.ReadMessageAsync();
            Assert.Equal(expected, result);
        }
    }

    // ── Unicode ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task WriteAndRead_Unicode_PreservesBytes()
    {
        // UTF-8 multibyte sequences must be counted in bytes, not chars.
        const string json = "{\"text\":\"こんにちは\",\"emoji\":\"🎉\"}";
        byte[] jsonBytes = Encoding.UTF8.GetBytes(json);

        var bytes = await WriteToBytes(json);

        // Verify Content-Length header matches byte count, not char count.
        string header = Encoding.ASCII.GetString(bytes, 0, bytes.Length - jsonBytes.Length);
        Assert.Contains($"Content-Length: {jsonBytes.Length}", header);

        string? result = await ReaderFor(bytes).ReadMessageAsync();
        Assert.Equal(json, result);
    }

    // ── EOF handling ─────────────────────────────────────────────────────────

    [Fact]
    public async Task Read_ReturnsNull_OnImmediateEof()
    {
        var reader = new JsonRpcReader(new MemoryStream([]));
        string? result = await reader.ReadMessageAsync();
        Assert.Null(result);
    }

    [Fact]
    public async Task Read_ReturnsRemainingMessage_ThenNull_OnEof()
    {
        const string json = "{\"x\":42}";
        var bytes = await WriteToBytes(json);
        var reader = ReaderFor(bytes);

        string? first = await reader.ReadMessageAsync();
        string? second = await reader.ReadMessageAsync();

        Assert.Equal(json, first);
        Assert.Null(second);
    }

    // ── Framing format ───────────────────────────────────────────────────────

    [Fact]
    public async Task Writer_ProducesCorrectFraming()
    {
        const string json = "{\"id\":1}";
        byte[] bytes = await WriteToBytes(json);
        string raw = Encoding.ASCII.GetString(bytes, 0, bytes.Length - Encoding.UTF8.GetByteCount(json));

        Assert.Equal($"Content-Length: {Encoding.UTF8.GetByteCount(json)}\r\n\r\n", raw);
    }

    // ── Large messages ───────────────────────────────────────────────────────

    [Fact]
    public async Task WriteAndRead_LargeMessage()
    {
        // 500 KB of JSON
        string json = "{\"data\":\"" + new string('x', 500_000) + "\"}";
        var bytes = await WriteToBytes(json);
        string? result = await ReaderFor(bytes).ReadMessageAsync();
        Assert.Equal(json, result);
    }

    // ── Concurrency ──────────────────────────────────────────────────────────

    [Fact]
    public async Task Writer_ConcurrentWrites_ProducesValidFraming()
    {
        using var ms     = new MemoryStream();
        using var writer = new JsonRpcWriter(ms);

        string[] messages = Enumerable.Range(0, 20).Select(i => $"{{\"i\":{i}}}").ToArray();
        await Task.WhenAll(messages.Select(m => writer.WriteAsync(m)));

        ms.Position = 0;
        var reader = new JsonRpcReader(ms);
        var received = new List<string?>();
        for (int i = 0; i < messages.Length; i++)
            received.Add(await reader.ReadMessageAsync());

        // All 20 messages must have been received (order may differ due to concurrency).
        Assert.Equal(messages.Length, received.Count);
        Assert.DoesNotContain(null, received);
        foreach (var msg in messages) Assert.Contains(msg, received!);
    }
}
