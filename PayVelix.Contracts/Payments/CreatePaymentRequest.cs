using System.Text.Json.Serialization;

namespace PayVelix.Contracts.Payments;

public sealed class CreatePaymentRequest
{
    [JsonIgnore]
    public string? IdempotencyKey { get; set; }

    public string? ReturnUrl { get; set; }

    public decimal Amount { get; set; }

    public string Currency { get; set; } = "USDT";

    public string? WebhookUrl { get; set; }

    public Dictionary<string, string>? CallbackParams { get; set; }
}
