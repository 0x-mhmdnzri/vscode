namespace CSharpSense.Protocol.Lsp;

public sealed class CompletionParams
{
    public required TextDocumentIdentifier TextDocument   { get; init; }
    public required Position               Position       { get; init; }
    public CompletionTriggerKind?          TriggerKind    { get; init; }
    public string?                         TriggerCharacter { get; init; }
}

public enum CompletionTriggerKind
{
    Invoked                           = 1,
    TriggerCharacter                  = 2,
    TriggerForIncompleteCompletions   = 3,
}

public sealed class CompletionList
{
    public bool IsIncomplete { get; init; }
    public required IReadOnlyList<CompletionItem> Items { get; init; }
}

public sealed class CompletionItem
{
    public required string         Label      { get; init; }
    public CompletionItemKind?     Kind       { get; init; }
    public string?                 Detail     { get; init; }
    public string?                 Documentation { get; init; }
    public string?                 SortText   { get; init; }
    public string?                 FilterText { get; init; }
    public string?                 InsertText { get; init; }
}

public enum CompletionItemKind
{
    Text          =  1, Method        =  2, Function      =  3,
    Constructor   =  4, Field         =  5, Variable      =  6,
    Class         =  7, Interface     =  8, Module        =  9,
    Property      = 10, Unit          = 11, Value         = 12,
    Enum          = 13, Keyword       = 14, Snippet       = 15,
    Color         = 16, File          = 17, Reference     = 18,
    Folder        = 19, EnumMember    = 20, Constant      = 21,
    Struct        = 22, Event         = 23, Operator      = 24,
    TypeParameter = 25,
}
