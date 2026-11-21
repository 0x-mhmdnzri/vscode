namespace CSharpSense.Protocol.JsonRpc;

public sealed record RpcError
{
    public int Code { get; init; }
    public string Message { get; init; } = string.Empty;

    public static class Codes
    {
        public const int ParseError = -32700;
        public const int InvalidRequest = -32600;
        public const int MethodNotFound = -32601;
        public const int InvalidParams = -32602;
        public const int InternalError = -32603;
        public const int ServerNotInitialized = -32002;
        public const int RequestCancelled = -32800;
    }
}
