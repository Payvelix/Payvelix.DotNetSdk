using Microsoft.Extensions.Options;
using PayVelix.Balance;
using PayVelix.Internal;
using PayVelix.Options;
using PayVelix.Payments;

namespace PayVelix;

internal sealed class PayVelixClientFactory : IPayVelixClientFactory
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IOptions<PayVelixOptions> _options;

    public PayVelixClientFactory(
        IHttpClientFactory httpClientFactory,
        IOptions<PayVelixOptions> options)
    {
        _httpClientFactory = httpClientFactory;
        _options = options;
    }

    public IPayVelixClient CreateClient(string apiKey)
    {
        return CreateClient(new PayVelixClientConfiguration
        {
            ApiKey = apiKey
        });
    }

    public IPayVelixClient CreateClient(PayVelixClientConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        var globalOptions = _options.Value;
        var apiKey = PayVelixConfigurationValidator.ValidateApiKey(configuration.ApiKey);
        var baseUrl = PayVelixConfigurationValidator.ValidateBaseUrl(
            configuration.BaseUrl ?? globalOptions.BaseUrl);
        var timeout = PayVelixConfigurationValidator.ValidateTimeout(
            configuration.Timeout ?? globalOptions.Timeout);

        var httpClient = _httpClientFactory.CreateClient(PayVelixHttp.HttpClientName);
        httpClient.BaseAddress = baseUrl;
        httpClient.Timeout = timeout;

        return CreateClient(httpClient, apiKey);
    }

    internal static IPayVelixClient CreateClient(HttpClient httpClient, string apiKey)
    {
        return new PayVelixClient(
            new PayVelixBalanceClient(httpClient, apiKey),
            new PayVelixPaymentsClient(httpClient, apiKey));
    }
}
