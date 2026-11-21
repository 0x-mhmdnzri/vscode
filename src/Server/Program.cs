using System.Text;
using CSharpSense.Protocol.JsonRpc;
using CSharpSense.Server;

// stdout is exclusively for JSON-RPC. Redirect Console.Write to stderr so
// any accidental Console.Write calls don't corrupt the protocol stream.
Console.OutputEncoding = Encoding.UTF8;
Console.InputEncoding  = Encoding.UTF8;

var stdin  = Console.OpenStandardInput();
var stdout = Console.OpenStandardOutput();

using var reader     = new JsonRpcReader(stdin);
using var writer     = new JsonRpcWriter(stdout);
var       dispatcher = new Dispatcher(writer);

using var cts = new CancellationTokenSource();
Console.CancelKeyPress += (_, e) => { e.Cancel = true; cts.Cancel(); };

try
{
    while (!cts.IsCancellationRequested && !dispatcher.ShouldExit)
    {
        string? message = await reader.ReadMessageAsync(cts.Token);
        if (message is null) break;  // client closed the pipe

        await dispatcher.HandleAsync(message, cts.Token);
    }
}
catch (OperationCanceledException) { /* clean shutdown */ }
catch (Exception ex)
{
    // Write to stderr so it doesn't corrupt the stdout JSON-RPC channel.
    await Console.Error.WriteLineAsync($"[fatal] {ex}");
    Environment.Exit(1);
}

Environment.Exit(dispatcher.ExitCode);
