using System.Text.Json;
using System.Text.Json.Serialization;

namespace PayVelix.Contracts.Balance;

public sealed class BalanceResponse
{
    public Dictionary<string, decimal>? Currencies { get; set; }

    public decimal UsdEquivalent { get; set; }

    [JsonExtensionData]
    public Dictionary<string, JsonElement>? AdditionalData { get; set; }
}
