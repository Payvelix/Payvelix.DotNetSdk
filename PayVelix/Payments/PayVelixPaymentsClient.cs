using System.Net.Http.Json;
using PayVelix.Contracts.Payments;
using PayVelix.Internal;

namespace PayVelix.Payments;

internal sealed class PayVelixPaymentsClient : IPayVelixPaymentsClient
{
    private const string CreatePaymentPath = "/api/Payments/Create";

    private readonly HttpClient _httpClient;

    public PayVelixPaymentsClient(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<CreatePaymentResponse> CreateAsync(
        CreatePaymentRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        return await CreateAsync(
            request,
            request.IdempotencyKey ?? string.Empty,
            cancellationToken);
    }

    public async Task<CreatePaymentResponse> CreateAsync(
        CreatePaymentRequest request,
        string idempotencyKey,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (request.Amount <= 0)
        {
            throw new ArgumentException("Amount must be greater than zero.", nameof(request));
        }

        if (string.IsNullOrWhiteSpace(idempotencyKey))
        {
            throw new ArgumentException("Idempotency key is required for payment creation.", nameof(idempotencyKey));
        }

        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, CreatePaymentPath)
        {
            Content = JsonContent.Create(
                request,
                options: PayVelixHttp.JsonOptions)
        };

        httpRequest.Headers.Add("Idempotency-Key", idempotencyKey);

        using var response = await _httpClient.SendAsync(httpRequest, cancellationToken);

        var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);

        if (response.IsSuccessStatusCode)
        {
            return PayVelixHttp.DeserializeSuccessResponse<CreatePaymentResponse>(
                responseBody,
                response.StatusCode,
                "create payment");
        }

        throw PayVelixHttp.CreateApiException(response.StatusCode, responseBody);
    }

    public async Task<VerifyPaymentResponse> VerifyAsync(
        string paymentId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(paymentId))
        {
            throw new ArgumentException("PaymentId is required.", nameof(paymentId));
        }

        if (!Guid.TryParse(paymentId, out var parsedPaymentId))
        {
            throw new ArgumentException("PaymentId must be a valid GUID.", nameof(paymentId));
        }

        return await VerifyAsync(parsedPaymentId, cancellationToken);
    }

    public async Task<VerifyPaymentResponse> VerifyAsync(
        Guid paymentId,
        CancellationToken cancellationToken = default)
    {
        if (paymentId == Guid.Empty)
        {
            throw new ArgumentException("PaymentId must not be empty.", nameof(paymentId));
        }

        var path = $"/api/Payments/{paymentId:D}/Verify";

        using var response = await _httpClient.GetAsync(path, cancellationToken);

        var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);

        if (response.IsSuccessStatusCode)
        {
            return PayVelixHttp.DeserializeSuccessResponse<VerifyPaymentResponse>(
                responseBody,
                response.StatusCode,
                "verify payment");
        }

        throw PayVelixHttp.CreateApiException(response.StatusCode, responseBody);
    }
}
