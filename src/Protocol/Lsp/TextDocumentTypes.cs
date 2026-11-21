namespace CSharpSense.Protocol.Lsp;

public sealed class TextDocumentItem
{
    public required string Uri        { get; init; }
    public required string LanguageId { get; init; }
    public int             Version    { get; init; }
    public required string Text       { get; init; }
}

public sealed class DidOpenTextDocumentParams
{
    public required TextDocumentItem TextDocument { get; init; }
}

public sealed class DidChangeTextDocumentParams
{
    public required VersionedTextDocumentIdentifier           TextDocument   { get; init; }
    public required IReadOnlyList<TextDocumentContentChangeEvent> ContentChanges { get; init; }
}

public sealed class TextDocumentContentChangeEvent
{
    /// <summary>Null for a full-document replacement.</summary>
    public Range?  Range { get; init; }
    public required string Text  { get; init; }
}

public sealed class DidCloseTextDocumentParams
{
    public required TextDocumentIdentifier TextDocument { get; init; }
}
