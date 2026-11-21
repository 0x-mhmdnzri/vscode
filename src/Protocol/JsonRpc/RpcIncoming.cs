using System.Text.Json;
using System.Text.Json.Serialization;

namespace CSharpSense.Protocol.JsonRpc;

/// <summary>Deserialisation target for any incoming JSON-RPC message.</summary>
/// <remarks>
/// <see cref="Id"/> and <see cref="Params"/> use <see cref="JsonElement"/> so the
/// dispatcher can inspect the discriminator before fully deserialising params.
/// When a field is absent from the JSON its <see cref="JsonElement.ValueKind"/>
/// will be <see cref="JsonValueKind.Undefined"/>.
/// </remarks>
public sealed class RpcIncoming
{
    [JsonPropertyName("jsonrpc")] public string JsonRpc { get; set; } = "2.0";
    [JsonPropertyName("id")]     public JsonElement Id     { get; set; }
    [JsonPropertyName("method")] public string?    Method { get; set; }
    [JsonPropertyName("params")] public JsonElement Params { get; set; }

    public bool IsRequest =>
        Method is not null &&
        Id.ValueKind is not JsonValueKind.Undefined and not JsonValueKind.Null;

    public bool IsNotification =>
        Method is not null &&
        Id.ValueKind is JsonValueKind.Undefined or JsonValueKind.Null;
}
