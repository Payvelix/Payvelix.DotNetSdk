using PayVelix.Contracts.Balance;
using PayVelix.Internal;

namespace PayVelix.Balance;

internal sealed class PayVelixBalanceClient : IPayVelixBalanceClient
{
    private readonly HttpClient _httpClient;
    private readonly string _apiKey;

    public PayVelixBalanceClient(HttpClient httpClient, string apiKey)
    {
        _httpClient = httpClient;
        _apiKey = PayVelixConfigurationValidator.ValidateApiKey(apiKey);
    }

    public async Task<BalanceResponse> GetAsync(
        string? id = null,
        CancellationToken cancellationToken = default)
    {
        var path = string.IsNullOrWhiteSpace(id)
            ? "/api/Balance"
            : $"/api/Balance?id={Uri.EscapeDataString(id)}";

        using var httpRequest = new HttpRequestMessage(HttpMethod.Get, path);
        PayVelixHttp.AddApiKey(httpRequest, _apiKey);

        using var response = await _httpClient.SendAsync(httpRequest, cancellationToken);

        var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);

        if (response.IsSuccessStatusCode)
        {
            return PayVelixHttp.DeserializeSuccessResponse<BalanceResponse>(
                responseBody,
                response.StatusCode,
                "balance");
        }

        throw PayVelixHttp.CreateApiException(response.StatusCode, responseBody, _apiKey);
    }
}
