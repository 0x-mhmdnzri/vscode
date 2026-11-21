using System.Text.Json;
using CSharpSense.Protocol;
using CSharpSense.Protocol.JsonRpc;
using CSharpSense.Protocol.Lsp;

namespace CSharpSense.Server;

/// <summary>
/// Routes incoming JSON-RPC messages to the correct handler and writes responses.
/// All state access is serialised — callers must not invoke concurrently.
/// </summary>
public sealed class Dispatcher
{
    private readonly JsonRpcWriter _writer;
    private bool _initialized;
    private bool _shutdownRequested;

    public bool ShouldExit { get; private set; }
    public int  ExitCode   { get; private set; }

    public Dispatcher(JsonRpcWriter writer) => _writer = writer;

    public Task HandleAsync(string json, CancellationToken ct)
    {
        RpcIncoming msg;
        try
        {
            msg = LspSerializer.Deserialize<RpcIncoming>(json)
                  ?? throw new InvalidOperationException("Deserialised to null.");
        }
        catch (Exception ex)
        {
            return SendErrorAsync(
                default,
                RpcError.Codes.ParseError,
                $"Parse error: {ex.Message}",
                ct);
        }

        if (msg.Method is null) return Task.CompletedTask; // malformed or response — ignore

        return msg.IsRequest
            ? DispatchRequestAsync(msg, ct)
            : DispatchNotificationAsync(msg, ct);
    }

    // ── Requests ────────────────────────────────────────────────────────────

    private Task DispatchRequestAsync(RpcIncoming msg, CancellationToken ct) =>
        msg.Method switch
        {
            "initialize"            => HandleInitializeAsync(msg, ct),
            "shutdown"              => HandleShutdownAsync(msg, ct),
            "textDocument/completion" => HandleCompletionAsync(msg, ct),
            _ => SendErrorAsync(
                     msg.Id,
                     RpcError.Codes.MethodNotFound,
                     $"Method not found: {msg.Method}",
                     ct)
        };

    private async Task HandleInitializeAsync(RpcIncoming msg, CancellationToken ct)
    {
        // initialize is valid even before _initialized — it IS the initialisation.
        var result = new InitializeResult
        {
            ServerInfo = new ServerInfo { Name = "CSharpSense", Version = "0.1.0" },
            Capabilities = new ServerCapabilities
            {
                TextDocumentSync   = TextDocumentSyncKind.Incremental,
                CompletionProvider = new CompletionOptions
                {
                    TriggerCharacters = [".", " ", "("],
                    ResolveProvider   = false,
                }
            }
        };
        _initialized = true;
        await SendResponseAsync(msg.Id, result, ct);
    }

    private async Task HandleShutdownAsync(RpcIncoming msg, CancellationToken ct)
    {
        _shutdownRequested = true;
        await SendNullResponseAsync(msg.Id, ct);
    }

    private async Task HandleCompletionAsync(RpcIncoming msg, CancellationToken ct)
    {
        if (!_initialized)
        {
            await SendErrorAsync(
                msg.Id,
                RpcError.Codes.ServerNotInitialized,
                "Server not initialized.",
                ct);
            return;
        }

        // M1: return an empty list; real completion added in M8.
        var result = new CompletionList { IsIncomplete = false, Items = [] };
        await SendResponseAsync(msg.Id, result, ct);
    }

    // ── Notifications ────────────────────────────────────────────────────────

    private Task DispatchNotificationAsync(RpcIncoming msg, CancellationToken ct) =>
        msg.Method switch
        {
            "initialized"              => HandleInitializedAsync(msg, ct),
            "exit"                     => HandleExitAsync(msg, ct),
            "textDocument/didOpen"     => HandleDidOpenAsync(msg, ct),
            "textDocument/didChange"   => HandleDidChangeAsync(msg, ct),
            "textDocument/didClose"    => HandleDidCloseAsync(msg, ct),
            "$/cancelRequest"          => Task.CompletedTask, // no-op until M8 adds cancellation
            _                          => Task.CompletedTask, // unknown notifications silently ignored
        };

    private Task HandleInitializedAsync(RpcIncoming msg, CancellationToken ct)
    {
        // Server is now fully ready; background indexing will start here in M5+.
        return Task.CompletedTask;
    }

    private Task HandleExitAsync(RpcIncoming msg, CancellationToken ct)
    {
        ShouldExit = true;
        ExitCode   = _shutdownRequested ? 0 : 1;
        return Task.CompletedTask;
    }

    private Task HandleDidOpenAsync(RpcIncoming msg, CancellationToken ct)
    {
        // M1 no-op; Workspace integration added in M8.
        return Task.CompletedTask;
    }

    private Task HandleDidChangeAsync(RpcIncoming msg, CancellationToken ct)
    {
        return Task.CompletedTask;
    }

    private Task HandleDidCloseAsync(RpcIncoming msg, CancellationToken ct)
    {
        return Task.CompletedTask;
    }

    // ── Response helpers ─────────────────────────────────────────────────────

    private Task SendResponseAsync<T>(JsonElement id, T result, CancellationToken ct)
    {
        string resultJson = LspSerializer.Serialize(result);
        return _writer.WriteAsync(BuildResponse(id, resultJson), ct);
    }

    // The 'shutdown' result must be JSON null, not omitted.
    private Task SendNullResponseAsync(JsonElement id, CancellationToken ct) =>
        _writer.WriteAsync(BuildResponse(id, "null"), ct);

    private Task SendErrorAsync(JsonElement id, int code, string message, CancellationToken ct)
    {
        string msg = System.Text.Json.JsonSerializer.Serialize(message);
        string json = $"{{\"jsonrpc\":\"2.0\",\"id\":{RawId(id)},\"error\":{{\"code\":{code},\"message\":{msg}}}}}";
        return _writer.WriteAsync(json, ct);
    }

    private static string BuildResponse(JsonElement id, string resultJson) =>
        $"{{\"jsonrpc\":\"2.0\",\"id\":{RawId(id)},\"result\":{resultJson}}}";

    private static string RawId(JsonElement id) =>
        id.ValueKind == JsonValueKind.Undefined ? "null" : id.GetRawText();
}
