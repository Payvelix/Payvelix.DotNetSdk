using PayVelix.Contracts.Balance;
using PayVelix.Internal;

namespace PayVelix.Balance;

internal sealed class PayVelixBalanceClient : IPayVelixBalanceClient
{
    private readonly HttpClient _httpClient;

    public PayVelixBalanceClient(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<BalanceResponse> GetAsync(
        string? id = null,
        CancellationToken cancellationToken = default)
    {
        var path = string.IsNullOrWhiteSpace(id)
            ? "/api/Balance"
            : $"/api/Balance?id={Uri.EscapeDataString(id)}";

        using var response = await _httpClient.GetAsync(path, cancellationToken);

        var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);

        if (response.IsSuccessStatusCode)
        {
            return PayVelixHttp.DeserializeSuccessResponse<BalanceResponse>(
                responseBody,
                response.StatusCode,
                "balance");
        }

        throw PayVelixHttp.CreateApiException(response.StatusCode, responseBody);
    }
}
