using System.Diagnostics;
using System.Text.Json;
using CSharpSense.Protocol.JsonRpc;
using CSharpSense.Server;

namespace CSharpSense.Server.Tests;

/// <summary>Spawns the real server executable and talks to it over stdio.</summary>
public sealed class StdioEndToEndTests
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(20);

    private sealed class ServerProcess : IDisposable
    {
        public Process Process { get; }
        public JsonRpcReader Reader { get; }
        public JsonRpcWriter Writer { get; }

        public ServerProcess()
        {
            string dll = typeof(Dispatcher).Assembly.Location;
            var psi = new ProcessStartInfo("dotnet", $"\"{dll}\"")
            {
                RedirectStandardInput  = true,
                RedirectStandardOutput = true,
                RedirectStandardError  = true,
                UseShellExecute        = false,
            };
            Process = Process.Start(psi) ?? throw new InvalidOperationException("Failed to start server.");
            Process.ErrorDataReceived += (_, _) => { };
            Process.BeginErrorReadLine();
            Reader = new JsonRpcReader(Process.StandardOutput.BaseStream);
            Writer = new JsonRpcWriter(Process.StandardInput.BaseStream);
        }

        public async Task<JsonElement> RequestAsync(int id, string method, string paramsJson)
        {
            await Writer.WriteAsync($"{{\"jsonrpc\":\"2.0\",\"id\":{id},\"method\":\"{method}\",\"params\":{paramsJson}}}");
            using var cts = new CancellationTokenSource(Timeout);
            string? json = await Reader.ReadMessageAsync(cts.Token);
            Assert.NotNull(json);
            using var doc = JsonDocument.Parse(json);
            Assert.Equal(id, doc.RootElement.GetProperty("id").GetInt32());
            return doc.RootElement.Clone();
        }

        public Task NotifyAsync(string method, string paramsJson) =>
            Writer.WriteAsync($"{{\"jsonrpc\":\"2.0\",\"method\":\"{method}\",\"params\":{paramsJson}}}");

        public void Dispose()
        {
            if (!Process.HasExited) Process.Kill(entireProcessTree: true);
            Process.Dispose();
            Writer.Dispose();
        }
    }

    [Fact]
    public async Task FullLifecycle_InitializeCompleteShutdownExit()
    {
        using var server = new ServerProcess();

        var init = await server.RequestAsync(1, "initialize", "{\"processId\":null,\"rootUri\":null,\"capabilities\":{}}");
        var caps = init.GetProperty("result").GetProperty("capabilities");
        Assert.Equal(2, caps.GetProperty("textDocumentSync").GetInt32());
        Assert.Contains(".", caps.GetProperty("completionProvider").GetProperty("triggerCharacters")
                                 .EnumerateArray().Select(e => e.GetString()));

        await server.NotifyAsync("initialized", "{}");
        await server.NotifyAsync("textDocument/didOpen",
            "{\"textDocument\":{\"uri\":\"file:///a.cs\",\"languageId\":\"csharp\",\"version\":1,\"text\":\"class A {}\"}}");

        var completion = await server.RequestAsync(2, "textDocument/completion",
            "{\"textDocument\":{\"uri\":\"file:///a.cs\"},\"position\":{\"line\":0,\"character\":9}}");
        var result = completion.GetProperty("result");
        Assert.False(result.GetProperty("isIncomplete").GetBoolean());
        Assert.Equal(0, result.GetProperty("items").GetArrayLength());

        var shutdown = await server.RequestAsync(3, "shutdown", "null");
        Assert.Equal(JsonValueKind.Null, shutdown.GetProperty("result").ValueKind);

        await server.NotifyAsync("exit", "null");
        Assert.True(server.Process.WaitForExit(Timeout), "Server did not exit after 'exit'.");
        Assert.Equal(0, server.Process.ExitCode);
    }

    [Fact]
    public async Task Exit_WithoutShutdown_ReturnsExitCode1()
    {
        using var server = new ServerProcess();
        await server.RequestAsync(1, "initialize", "{\"capabilities\":{}}");
        await server.NotifyAsync("exit", "null");
        Assert.True(server.Process.WaitForExit(Timeout));
        Assert.Equal(1, server.Process.ExitCode);
    }

    [Fact]
    public async Task UnknownRequest_ReturnsMethodNotFound()
    {
        using var server = new ServerProcess();
        var resp = await server.RequestAsync(7, "textDocument/nonexistent", "{}");
        Assert.Equal(RpcError.Codes.MethodNotFound, resp.GetProperty("error").GetProperty("code").GetInt32());
    }

    [Fact]
    public async Task Completion_BeforeInitialize_ReturnsServerNotInitialized()
    {
        using var server = new ServerProcess();
        var resp = await server.RequestAsync(1, "textDocument/completion",
            "{\"textDocument\":{\"uri\":\"file:///a.cs\"},\"position\":{\"line\":0,\"character\":0}}");
        Assert.Equal(RpcError.Codes.ServerNotInitialized, resp.GetProperty("error").GetProperty("code").GetInt32());
    }

    [Fact]
    public async Task ClientClosesStdin_ServerExits()
    {
        using var server = new ServerProcess();
        await server.RequestAsync(1, "initialize", "{\"capabilities\":{}}");
        server.Process.StandardInput.Close();
        Assert.True(server.Process.WaitForExit(Timeout), "Server did not exit on stdin EOF.");
    }
}
