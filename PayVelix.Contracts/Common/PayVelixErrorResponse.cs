using System.Text.Json;
using System.Text.Json.Serialization;

namespace PayVelix.Contracts.Common;

public sealed class PayVelixErrorResponse
{
    public string? Code { get; set; }

    public string? Message { get; set; }

    public string? RequestId { get; set; }

    public JsonElement? Details { get; set; }

    [JsonExtensionData]
    public Dictionary<string, JsonElement>? AdditionalData { get; set; }
}
