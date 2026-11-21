import * as vscode from 'vscode';
import { spawn } from 'child_process';
import * as path from 'path';
import { JsonRpcTransport } from './transport';

let transport: JsonRpcTransport | undefined;

export async function activate(context: vscode.ExtensionContext): Promise<void> {
    const output = vscode.window.createOutputChannel('CSharpSense');
    context.subscriptions.push(output);

    const config  = vscode.workspace.getConfiguration('csharpsense');
    const trace   = config.get<boolean>('trace', false);
    const cfgPath = config.get<string>('serverPath', '');

    const serverDll = cfgPath.length > 0
        ? cfgPath
        : context.asAbsolutePath(path.join('server', 'CSharpSense.Server.dll'));

    output.appendLine(`CSharpSense activating — server: ${serverDll}`);

    const workspaceRoot = vscode.workspace.workspaceFolders?.[0]?.uri.fsPath;

    let proc;
    try {
        proc = spawn('dotnet', [serverDll], {
            stdio: ['pipe', 'pipe', 'pipe'],
            cwd:   workspaceRoot,
        });
    } catch (err) {
        output.appendLine(`Failed to spawn server: ${err}`);
        void vscode.window.showErrorMessage(
            'CSharpSense: could not start the server. Is .NET 9+ installed?'
        );
        return;
    }

    transport = new JsonRpcTransport(proc, {
        trace,
        log: (msg) => output.appendLine(msg),
    });

    // ── Initialize handshake ──────────────────────────────────────────────────

    try {
        const initResult = await transport.request('initialize', {
            processId: process.pid,
            rootUri:   workspaceRoot
                ? vscode.Uri.file(workspaceRoot).toString()
                : null,
            capabilities: {
                textDocument: {
                    completion: { completionItem: { snippetSupport: false } }
                }
            }
        });
        output.appendLine(`Server initialized: ${JSON.stringify(initResult)}`);
    } catch (err) {
        output.appendLine(`Initialize failed: ${err}`);
        void vscode.window.showErrorMessage('CSharpSense: server initialization failed.');
        transport.dispose();
        transport = undefined;
        return;
    }

    transport.notify('initialized', {});

    // ── Document sync ─────────────────────────────────────────────────────────

    for (const doc of vscode.workspace.textDocuments) {
        if (doc.languageId === 'csharp') notifyOpen(transport, doc);
    }

    context.subscriptions.push(
        vscode.workspace.onDidOpenTextDocument((doc) => {
            if (doc.languageId === 'csharp') notifyOpen(transport!, doc);
        }),
        vscode.workspace.onDidChangeTextDocument((e) => {
            if (e.document.languageId === 'csharp') notifyChange(transport!, e);
        }),
        vscode.workspace.onDidCloseTextDocument((doc) => {
            if (doc.languageId === 'csharp') notifyClose(transport!, doc);
        }),
    );

    // ── Completion provider ───────────────────────────────────────────────────

    context.subscriptions.push(
        vscode.languages.registerCompletionItemProvider(
            { language: 'csharp', scheme: 'file' },
            {
                async provideCompletionItems(document, position) {
                    if (!transport) return [];
                    try {
                        const result = await transport.request('textDocument/completion', {
                            textDocument: { uri: document.uri.toString() },
                            position:     { line: position.line, character: position.character },
                            context:      { triggerKind: 1 },
                        });
                        return toCompletionItems(result);
                    } catch (err) {
                        output.appendLine(`Completion error: ${err}`);
                        return [];
                    }
                }
            },
            '.', ' ', '('
        )
    );

    output.appendLine('CSharpSense ready.');
}

export function deactivate(): Thenable<void> | undefined {
    if (!transport) return undefined;
    const t = transport;
    transport = undefined;
    return t.request('shutdown', null)
        .then(() => { t.notify('exit', null); })
        .catch(() => { /* server may already be gone */ })
        .finally(() => { t.dispose(); });
}

// ── Document sync helpers ─────────────────────────────────────────────────────

function notifyOpen(t: JsonRpcTransport, doc: vscode.TextDocument): void {
    t.notify('textDocument/didOpen', {
        textDocument: {
            uri:        doc.uri.toString(),
            languageId: doc.languageId,
            version:    doc.version,
            text:       doc.getText(),
        }
    });
}

function notifyChange(
    t: JsonRpcTransport,
    e: vscode.TextDocumentChangeEvent
): void {
    t.notify('textDocument/didChange', {
        textDocument:   { uri: e.document.uri.toString(), version: e.document.version },
        contentChanges: e.contentChanges.map((c) => ({
            range: {
                start: { line: c.range.start.line, character: c.range.start.character },
                end:   { line: c.range.end.line,   character: c.range.end.character   },
            },
            text: c.text,
        })),
    });
}

function notifyClose(t: JsonRpcTransport, doc: vscode.TextDocument): void {
    t.notify('textDocument/didClose', {
        textDocument: { uri: doc.uri.toString() }
    });
}

// ── LSP → VS Code type mapping ────────────────────────────────────────────────

interface LspCompletionItem {
    label:          string;
    kind?:          number;
    detail?:        string;
    documentation?: string;
    sortText?:      string;
    filterText?:    string;
    insertText?:    string;
}

type LspCompletionResult =
    | LspCompletionItem[]
    | { isIncomplete: boolean; items: LspCompletionItem[] }
    | null;

function toCompletionItems(result: unknown): vscode.CompletionItem[] {
    if (!result) return [];
    const items: LspCompletionItem[] = Array.isArray(result)
        ? (result as LspCompletionItem[])
        : ((result as { items?: LspCompletionItem[] }).items ?? []);

    return items.map((item) => {
        const vsItem = new vscode.CompletionItem(item.label, mapKind(item.kind));
        if (item.detail)        vsItem.detail        = item.detail;
        if (item.documentation) vsItem.documentation  = item.documentation;
        if (item.sortText)      vsItem.sortText       = item.sortText;
        if (item.filterText)    vsItem.filterText     = item.filterText;
        if (item.insertText)    vsItem.insertText     = item.insertText;
        return vsItem;
    });
}

function mapKind(kind?: number): vscode.CompletionItemKind {
    switch (kind) {
        case  2: return vscode.CompletionItemKind.Method;
        case  3: return vscode.CompletionItemKind.Function;
        case  4: return vscode.CompletionItemKind.Constructor;
        case  5: return vscode.CompletionItemKind.Field;
        case  6: return vscode.CompletionItemKind.Variable;
        case  7: return vscode.CompletionItemKind.Class;
        case  8: return vscode.CompletionItemKind.Interface;
        case  9: return vscode.CompletionItemKind.Module;
        case 10: return vscode.CompletionItemKind.Property;
        case 13: return vscode.CompletionItemKind.Enum;
        case 14: return vscode.CompletionItemKind.Keyword;
        case 20: return vscode.CompletionItemKind.EnumMember;
        case 21: return vscode.CompletionItemKind.Constant;
        case 22: return vscode.CompletionItemKind.Struct;
        default: return vscode.CompletionItemKind.Text;
    }
}
