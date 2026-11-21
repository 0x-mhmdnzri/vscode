using System.Text.Json;
using System.Text.Json.Serialization;

namespace CSharpSense.Protocol;

/// <summary>
/// Shared <see cref="JsonSerializerOptions"/> for all LSP body types.
/// Uses camelCase property names (matching the LSP spec) and omits null
/// optional fields from outbound messages.
/// </summary>
public static class LspSerializer
{
    public static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy         = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive  = true,
        DefaultIgnoreCondition       = JsonIgnoreCondition.WhenWritingNull,
        WriteIndented                = false,
        NumberHandling               = JsonNumberHandling.AllowReadingFromString,
    };

    public static string Serialize<T>(T value) =>
        JsonSerializer.Serialize(value, Options);

    public static T? Deserialize<T>(string json) =>
        JsonSerializer.Deserialize<T>(json, Options);

    public static T? Deserialize<T>(System.Text.Json.JsonElement element) =>
        element.Deserialize<T>(Options);
}
