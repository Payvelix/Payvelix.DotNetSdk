using System.Net;
using Microsoft.Extensions.DependencyInjection;
using PayVelix.Balance;
using PayVelix.Contracts.Common;
using PayVelix.DependencyInjection;

namespace PayVelix.Tests.Balance;

public sealed class BalanceTests
{
    [Fact]
    public async Task GetAsync_SendsGetToBalanceEndpoint()
    {
        using var handler = new FakeHttpMessageHandler(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = JsonContent("""{"currencies":{"USD":10},"usdEquivalent":10}""")
        });
        var client = CreateClient(handler);

        await client.GetAsync();

        Assert.Equal(HttpMethod.Get, handler.Request?.Method);
        Assert.Equal("/api/Balance", handler.Request?.RequestUri?.PathAndQuery);
    }

    [Fact]
    public async Task GetAsync_UriEscapesId()
    {
        using var handler = new FakeHttpMessageHandler(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = JsonContent("""{"currencies":{"USD":10},"usdEquivalent":10}""")
        });
        var client = CreateClient(handler);

        await client.GetAsync("balance id/with+symbols?");

        Assert.Equal(
            "/api/Balance?id=balance%20id%2Fwith%2Bsymbols%3F",
            handler.Request?.RequestUri?.PathAndQuery);
    }

    [Fact]
    public async Task GetAsync_DeserializesSuccessfulResponse()
    {
        using var handler = new FakeHttpMessageHandler(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = JsonContent(
                """
                {
                    "currencies": {
                        "BTC": 0.1,
                        "USDT": 25.5
                    },
                    "usdEquivalent": 1025.5,
                    "providerTraceId": "trace_123"
                }
                """)
        });
        var client = CreateClient(handler);

        var response = await client.GetAsync("wallet_123");

        Assert.NotNull(response.Currencies);
        Assert.Equal(0.1m, response.Currencies["BTC"]);
        Assert.Equal(25.5m, response.Currencies["USDT"]);
        Assert.Equal(1025.5m, response.UsdEquivalent);
        Assert.NotNull(response.AdditionalData);
        Assert.True(response.AdditionalData.ContainsKey("providerTraceId"));
    }

    [Fact]
    public async Task GetAsync_ThrowsPayVelixApiExceptionOnNonSuccessStatus()
    {
        using var handler = new FakeHttpMessageHandler(new HttpResponseMessage(HttpStatusCode.Unauthorized)
        {
            Content = JsonContent("""{"code":"unauthorized","message":"Invalid API key."}""")
        });
        var client = CreateClient(handler);

        var exception = await Assert.ThrowsAsync<PayVelixApiException>(() =>
            client.GetAsync());

        Assert.Equal(HttpStatusCode.Unauthorized, exception.StatusCode);
        Assert.Equal("unauthorized", exception.ErrorCode);
        Assert.Equal("Invalid API key.", exception.Message);
    }

    [Fact]
    public async Task AddPayVelix_SendsApiKeyHeader()
    {
        using var handler = new FakeHttpMessageHandler(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = JsonContent("""{"currencies":{"USD":10},"usdEquivalent":10}""")
        });
        var services = new ServiceCollection();

        services.AddPayVelix(options =>
        {
            options.ApiKey = "pv_test_key";
        });
        services
            .AddHttpClient<IPayVelixBalanceClient, PayVelixBalanceClient>()
            .ConfigurePrimaryHttpMessageHandler(() => handler);

        using var serviceProvider = services.BuildServiceProvider();
        var payVelix = serviceProvider.GetRequiredService<IPayVelixClient>();

        await payVelix.Balance.GetAsync();

        Assert.NotNull(handler.Request);
        Assert.True(handler.Request.Headers.TryGetValues("X-Api-Key", out var values));
        Assert.Equal("pv_test_key", Assert.Single(values));
    }

    private static IPayVelixBalanceClient CreateClient(HttpMessageHandler handler)
    {
        var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://api.payvelix.com")
        };

        return new PayVelixBalanceClient(httpClient);
    }

    private static StringContent JsonContent(string json)
    {
        return new StringContent(json, System.Text.Encoding.UTF8, "application/json");
    }

    private sealed class FakeHttpMessageHandler : HttpMessageHandler
    {
        private readonly HttpResponseMessage _response;

        public FakeHttpMessageHandler(HttpResponseMessage response)
        {
            _response = response;
        }

        public HttpRequestMessage? Request { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            Request = request;
            return Task.FromResult(_response);
        }
    }
}
