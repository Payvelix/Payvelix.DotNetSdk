using System.Text.Json;
using System.Text.Json.Serialization;

namespace PayVelix.Contracts.Payments;

public sealed class VerifyPaymentResponse
{
    public Guid PaymentId { get; set; }

    public decimal Amount { get; set; }

    public decimal PaidAmount { get; set; }

    public decimal FeeAmount { get; set; }

    public decimal ExpectedAmount { get; set; }

    public decimal MerchantReceivableAmount { get; set; }

    public string? Currency { get; set; }

    public string? Network { get; set; }

    public VerifyPaymentStatus Status { get; set; }

    public DateTimeOffset ExpiresAt { get; set; }

    [JsonExtensionData]
    public Dictionary<string, JsonElement>? AdditionalData { get; set; }
}
