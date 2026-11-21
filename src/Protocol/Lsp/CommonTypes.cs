namespace CSharpSense.Protocol.Lsp;

/// <summary>Zero-based line and character offset within a document.</summary>
public sealed record Position(int Line, int Character);

/// <summary>A range in a document denoted by start and end positions.</summary>
public sealed record Range(Position Start, Position End);

/// <summary>Identifies a text document by its URI.</summary>
public sealed record TextDocumentIdentifier(string Uri);

/// <summary>Identifies a versioned text document.</summary>
public sealed class VersionedTextDocumentIdentifier
{
    public required string Uri     { get; init; }
    public int             Version { get; init; }
}
