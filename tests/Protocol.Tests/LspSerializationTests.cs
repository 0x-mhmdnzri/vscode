using System.Text.Json;
using CSharpSense.Protocol;
using CSharpSense.Protocol.Lsp;

namespace CSharpSense.Protocol.Tests;

public sealed class LspSerializationTests
{
    // ── Position ─────────────────────────────────────────────────────────────

    [Fact]
    public void Position_SerializesCamelCase()
    {
        var pos  = new Position(3, 10);
        string json = LspSerializer.Serialize(pos);
        Assert.Equal("{\"line\":3,\"character\":10}", json);
    }

    [Fact]
    public void Position_RoundTrip()
    {
        var pos  = new Position(0, 15);
        var back = LspSerializer.Deserialize<Position>(LspSerializer.Serialize(pos));
        Assert.Equal(pos, back);
    }

    // ── CompletionList ────────────────────────────────────────────────────────

    [Fact]
    public void CompletionList_Empty_RoundTrip()
    {
        var list = new CompletionList { IsIncomplete = false, Items = [] };
        var back = LspSerializer.Deserialize<CompletionList>(LspSerializer.Serialize(list));
        Assert.NotNull(back);
        Assert.False(back.IsIncomplete);
        Assert.Empty(back.Items);
    }

    [Fact]
    public void CompletionItem_NullOptionals_OmittedFromJson()
    {
        var item = new CompletionItem { Label = "Foo", Kind = CompletionItemKind.Class };
        string json = LspSerializer.Serialize(item);

        // Optional nulls must not appear.
        Assert.DoesNotContain("\"detail\"", json);
        Assert.DoesNotContain("\"documentation\"", json);
        Assert.DoesNotContain("\"sortText\"", json);
    }

    [Fact]
    public void CompletionItem_AllFields_RoundTrip()
    {
        var item = new CompletionItem
        {
            Label         = "MyMethod",
            Kind          = CompletionItemKind.Method,
            Detail        = "void MyMethod()",
            Documentation = "Does something.",
            SortText      = "0_MyMethod",
            FilterText    = "MyMethod",
            InsertText    = "MyMethod($0)",
        };
        string json = LspSerializer.Serialize(item);
        var back = LspSerializer.Deserialize<CompletionItem>(json);
        Assert.NotNull(back);
        Assert.Equal("MyMethod", back.Label);
        Assert.Equal(CompletionItemKind.Method, back.Kind);
        Assert.Equal("void MyMethod()", back.Detail);
    }

    // ── InitializeResult ─────────────────────────────────────────────────────

    [Fact]
    public void InitializeResult_RoundTrip()
    {
        var result = new InitializeResult
        {
            ServerInfo = new ServerInfo { Name = "CSharpSense", Version = "0.1.0" },
            Capabilities = new ServerCapabilities
            {
                TextDocumentSync   = TextDocumentSyncKind.Incremental,
                CompletionProvider = new CompletionOptions
                {
                    TriggerCharacters = [".", "("],
                    ResolveProvider   = false,
                }
            }
        };

        string json = LspSerializer.Serialize(result);
        var back = LspSerializer.Deserialize<InitializeResult>(json);

        Assert.NotNull(back);
        Assert.Equal("CSharpSense", back.ServerInfo?.Name);
        Assert.Equal("0.1.0", back.ServerInfo?.Version);
        Assert.Equal(TextDocumentSyncKind.Incremental, back.Capabilities.TextDocumentSync);
        Assert.NotNull(back.Capabilities.CompletionProvider?.TriggerCharacters);
        Assert.Equal([".", "("], back.Capabilities.CompletionProvider!.TriggerCharacters!);
    }

    // ── RpcIncoming discriminator ─────────────────────────────────────────────

    [Fact]
    public void RpcIncoming_Request_IsRequest()
    {
        string json = "{\"jsonrpc\":\"2.0\",\"id\":1,\"method\":\"initialize\",\"params\":{}}";
        var msg = LspSerializer.Deserialize<CSharpSense.Protocol.JsonRpc.RpcIncoming>(json);
        Assert.NotNull(msg);
        Assert.True(msg.IsRequest);
        Assert.False(msg.IsNotification);
        Assert.Equal("initialize", msg.Method);
        Assert.Equal(JsonValueKind.Number, msg.Id.ValueKind);
    }

    [Fact]
    public void RpcIncoming_Notification_IsNotification()
    {
        string json = "{\"jsonrpc\":\"2.0\",\"method\":\"initialized\",\"params\":{}}";
        var msg = LspSerializer.Deserialize<CSharpSense.Protocol.JsonRpc.RpcIncoming>(json);
        Assert.NotNull(msg);
        Assert.False(msg.IsRequest);
        Assert.True(msg.IsNotification);
    }

    [Fact]
    public void RpcIncoming_NullId_TreatedAsNotification()
    {
        string json = "{\"jsonrpc\":\"2.0\",\"id\":null,\"method\":\"$/cancelRequest\"}";
        var msg = LspSerializer.Deserialize<CSharpSense.Protocol.JsonRpc.RpcIncoming>(json);
        Assert.NotNull(msg);
        Assert.True(msg.IsNotification);
    }

    [Fact]
    public void RpcIncoming_StringId_IsRequest()
    {
        string json = "{\"jsonrpc\":\"2.0\",\"id\":\"req-42\",\"method\":\"shutdown\"}";
        var msg = LspSerializer.Deserialize<CSharpSense.Protocol.JsonRpc.RpcIncoming>(json);
        Assert.NotNull(msg);
        Assert.True(msg.IsRequest);
        Assert.Equal(JsonValueKind.String, msg.Id.ValueKind);
        Assert.Equal("req-42", msg.Id.GetString());
    }
}
