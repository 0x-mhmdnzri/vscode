namespace CSharpSense.Protocol.Lsp;

public sealed class InitializeParams
{
    public int?    ProcessId { get; init; }
    public string? RootUri   { get; init; }
    // ClientCapabilities is accepted but ignored for now.
}

public sealed class InitializeResult
{
    public ServerInfo?        ServerInfo   { get; init; }
    public required ServerCapabilities Capabilities { get; init; }
}

public sealed class ServerInfo
{
    public required string Name    { get; init; }
    public string?         Version { get; init; }
}

public sealed class ServerCapabilities
{
    public TextDocumentSyncKind TextDocumentSync  { get; init; } = TextDocumentSyncKind.Incremental;
    public CompletionOptions?   CompletionProvider { get; init; }
}

public enum TextDocumentSyncKind { None = 0, Full = 1, Incremental = 2 }

public sealed class CompletionOptions
{
    public string[]? TriggerCharacters { get; init; }
    public bool      ResolveProvider   { get; init; }
}
