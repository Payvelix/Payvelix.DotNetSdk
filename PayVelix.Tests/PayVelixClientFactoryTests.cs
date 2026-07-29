using System.Collections.Concurrent;
using System.Net;
using Microsoft.Extensions.DependencyInjection;
using PayVelix.Balance;
using PayVelix.Contracts.Common;
using PayVelix.Contracts.Payments;
using PayVelix.DependencyInjection;
using PayVelix.Internal;
using PayVelix.Payments;

namespace PayVelix.Tests;

public sealed class PayVelixClientFactoryTests
{
    private static readonly Guid PaymentId = Guid.Parse("123e4567-e89b-12d3-a456-426614174000");

    [Fact]
    public async Task AddPayVelix_ExistingGlobalClientStillSendsConfiguredApiKey()
    {
        using var handler = new RecordingHttpMessageHandler(_ => SuccessPaymentResponse());
        using var serviceProvider = CreateServiceProvider(handler, options =>
        {
            options.ApiKey = "global-key";
        });

        var client = serviceProvider.GetRequiredService<IPayVelixClient>();

        await client.Payments.CreateAsync(CreateRequest(), "idem-global");

        var request = Assert.Single(handler.Requests);
        Assert.Equal("global-key", request.GetHeader("X-Api-Key"));
        Assert.Null(request.DefaultApiKeyHeader);
    }

    [Fact]
    public void AddPayVelix_RegistersClientFactory()
    {
        using var handler = new RecordingHttpMessageHandler(_ => SuccessPaymentResponse());
        using var serviceProvider = CreateServiceProvider(handler, options =>
        {
            options.ApiKey = "global-key";
        });

        var factory = serviceProvider.GetRequiredService<IPayVelixClientFactory>();

        Assert.NotNull(factory);
    }

    [Fact]
    public async Task FactoryClient_SendsSuppliedMerchantApiKey()
    {
        using var handler = new RecordingHttpMessageHandler(_ => SuccessPaymentResponse());
        using var serviceProvider = CreateServiceProvider(handler, options =>
        {
            options.ApiKey = "global-key";
        });
        var factory = serviceProvider.GetRequiredService<IPayVelixClientFactory>();

        var client = factory.CreateClient("merchant-key");

        await client.Payments.CreateAsync(CreateRequest(), "idem-merchant");

        var request = Assert.Single(handler.Requests);
        Assert.Equal("merchant-key", request.GetHeader("X-Api-Key"));
        Assert.Null(request.DefaultApiKeyHeader);
    }

    [Fact]
    public async Task FactoryClients_DoNotLeakApiKeysBetweenConcurrentRequests()
    {
        using var handler = new RecordingHttpMessageHandler(async request =>
        {
            await Task.Delay(25);
            return SuccessPaymentResponse();
        });
        using var serviceProvider = CreateServiceProvider(handler, options =>
        {
            options.ApiKey = "global-key";
        });
        var factory = serviceProvider.GetRequiredService<IPayVelixClientFactory>();

        var clientA = factory.CreateClient("merchant-a-key");
        var clientB = factory.CreateClient("merchant-b-key");

        await Task.WhenAll(
            clientA.Payments.CreateAsync(CreateRequest(), "idem-a"),
            clientB.Payments.CreateAsync(CreateRequest(), "idem-b"));

        Assert.Contains(handler.Requests, request =>
            request.GetHeader("X-Api-Key") == "merchant-a-key"
            && request.GetHeader("Idempotency-Key") == "idem-a");
        Assert.Contains(handler.Requests, request =>
            request.GetHeader("X-Api-Key") == "merchant-b-key"
            && request.GetHeader("Idempotency-Key") == "idem-b");
        Assert.All(handler.Requests, request => Assert.Null(request.DefaultApiKeyHeader));
    }

    [Fact]
    public void FactoryClient_InheritsGlobalBaseUrlAndTimeout()
    {
        using var handler = new RecordingHttpMessageHandler(_ => SuccessPaymentResponse());
        using var serviceProvider = CreateServiceProvider(handler, options =>
        {
            options.ApiKey = "global-key";
            options.BaseUrl = "https://tenant-api.payvelix.test/root";
            options.Timeout = TimeSpan.FromSeconds(42);
        });
        var factory = serviceProvider.GetRequiredService<IPayVelixClientFactory>();

        var client = factory.CreateClient("merchant-key");
        var httpClient = GetHttpClient(client.Payments);

        Assert.Equal(new Uri("https://tenant-api.payvelix.test/root/"), httpClient.BaseAddress);
        Assert.Equal(TimeSpan.FromSeconds(42), httpClient.Timeout);
    }

    [Fact]
    public void FactoryClient_AppliesPerClientBaseUrlAndTimeoutOverrides()
    {
        using var handler = new RecordingHttpMessageHandler(_ => SuccessPaymentResponse());
        using var serviceProvider = CreateServiceProvider(handler, options =>
        {
            options.ApiKey = "global-key";
            options.BaseUrl = "https://api.payvelix.com";
            options.Timeout = TimeSpan.FromSeconds(30);
        });
        var factory = serviceProvider.GetRequiredService<IPayVelixClientFactory>();

        var client = factory.CreateClient(new PayVelixClientConfiguration
        {
            ApiKey = "merchant-key",
            BaseUrl = "https://sandbox.payvelix.test/api",
            Timeout = TimeSpan.FromSeconds(15)
        });
        var httpClient = GetHttpClient(client.Payments);

        Assert.Equal(new Uri("https://sandbox.payvelix.test/api/"), httpClient.BaseAddress);
        Assert.Equal(TimeSpan.FromSeconds(15), httpClient.Timeout);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void FactoryClient_RejectsBlankApiKeys(string? apiKey)
    {
        using var handler = new RecordingHttpMessageHandler(_ => SuccessPaymentResponse());
        using var serviceProvider = CreateServiceProvider(handler, options =>
        {
            options.ApiKey = "global-key";
        });
        var factory = serviceProvider.GetRequiredService<IPayVelixClientFactory>();

        var exception = Assert.Throws<ArgumentException>(() => factory.CreateClient(apiKey!));

        Assert.DoesNotContain("global-key", exception.Message);
    }

    [Theory]
    [InlineData("not-a-url")]
    [InlineData("ftp://api.payvelix.com")]
    public void FactoryClient_RejectsInvalidBaseUrls(string baseUrl)
    {
        using var handler = new RecordingHttpMessageHandler(_ => SuccessPaymentResponse());
        using var serviceProvider = CreateServiceProvider(handler, options =>
        {
            options.ApiKey = "global-key";
        });
        var factory = serviceProvider.GetRequiredService<IPayVelixClientFactory>();

        Assert.Throws<ArgumentException>(() => factory.CreateClient(new PayVelixClientConfiguration
        {
            ApiKey = "merchant-key",
            BaseUrl = baseUrl
        }));
    }

    [Fact]
    public async Task ApiKey_IsRedactedFromApiExceptionMessageAndBody()
    {
        const string apiKey = "merchant-secret-key";
        using var handler = new RecordingHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.Unauthorized)
        {
            Content = JsonContent($$"""{"code":"unauthorized","message":"Invalid key {{apiKey}}."}""")
        });
        using var serviceProvider = CreateServiceProvider(handler, options =>
        {
            options.ApiKey = "global-key";
        });
        var client = serviceProvider
            .GetRequiredService<IPayVelixClientFactory>()
            .CreateClient(apiKey);

        var exception = await Assert.ThrowsAsync<PayVelixApiException>(() =>
            client.Payments.CreateAsync(CreateRequest(), "idem-secret"));

        Assert.DoesNotContain(apiKey, exception.Message);
        Assert.DoesNotContain(apiKey, exception.ResponseBody);
    }

    [Fact]
    public async Task CreatePayment_SendsIdempotencyKeyAndApiKeyHeaders()
    {
        using var handler = new RecordingHttpMessageHandler(_ => SuccessPaymentResponse());
        using var serviceProvider = CreateServiceProvider(handler, options =>
        {
            options.ApiKey = "global-key";
        });
        var client = serviceProvider
            .GetRequiredService<IPayVelixClientFactory>()
            .CreateClient("merchant-key");

        await client.Payments.CreateAsync(CreateRequest(), "idem-123");

        var request = Assert.Single(handler.Requests);
        Assert.Equal("merchant-key", request.GetHeader("X-Api-Key"));
        Assert.Equal("idem-123", request.GetHeader("Idempotency-Key"));
    }

    [Fact]
    public async Task VerifyPayment_SendsPerClientApiKeyHeader()
    {
        using var handler = new RecordingHttpMessageHandler(_ => SuccessVerifyResponse());
        using var serviceProvider = CreateServiceProvider(handler, options =>
        {
            options.ApiKey = "global-key";
        });
        var client = serviceProvider
            .GetRequiredService<IPayVelixClientFactory>()
            .CreateClient("merchant-key");

        await client.Payments.VerifyAsync(PaymentId);

        var request = Assert.Single(handler.Requests);
        Assert.Equal("merchant-key", request.GetHeader("X-Api-Key"));
        Assert.Equal($"/api/Payments/{PaymentId:D}/Verify", request.PathAndQuery);
    }

    [Fact]
    public async Task Cancellation_RemainsOperationCanceledException()
    {
        using var handler = new RecordingHttpMessageHandler(async (_, cancellationToken) =>
        {
            await Task.Delay(TimeSpan.FromSeconds(30), cancellationToken);
            return SuccessPaymentResponse();
        });
        using var serviceProvider = CreateServiceProvider(handler, options =>
        {
            options.ApiKey = "global-key";
        });
        var client = serviceProvider
            .GetRequiredService<IPayVelixClientFactory>()
            .CreateClient("merchant-key");
        using var cancellationTokenSource = new CancellationTokenSource();
        cancellationTokenSource.Cancel();

        await Assert.ThrowsAsync<TaskCanceledException>(() =>
            client.Payments.CreateAsync(CreateRequest(), "idem-cancel", cancellationTokenSource.Token));
    }

    [Fact]
    public async Task FactoryClient_CanUsePaymentsAndBalanceApis()
    {
        using var handler = new RecordingHttpMessageHandler(request =>
            request.RequestUri?.AbsolutePath == "/api/Balance"
                ? SuccessBalanceResponse()
                : SuccessPaymentResponse());
        using var serviceProvider = CreateServiceProvider(handler, options =>
        {
            options.ApiKey = "global-key";
        });
        var client = serviceProvider
            .GetRequiredService<IPayVelixClientFactory>()
            .CreateClient("merchant-key");

        await client.Payments.CreateAsync(CreateRequest(), "idem-payment");
        var balance = await client.Balance.GetAsync();

        Assert.Equal(10, balance.UsdEquivalent);
        Assert.Contains(handler.Requests, request => request.PathAndQuery == "/api/Payments/Create");
        Assert.Contains(handler.Requests, request => request.PathAndQuery == "/api/Balance");
        Assert.All(handler.Requests, request => Assert.Equal("merchant-key", request.GetHeader("X-Api-Key")));
    }

    [Fact]
    public async Task FactoryClient_PreservesSdkGeneratedHeaders()
    {
        using var handler = new RecordingHttpMessageHandler(_ => SuccessPaymentResponse());
        using var serviceProvider = CreateServiceProvider(handler, options =>
        {
            options.ApiKey = "global-key";
        });
        var client = serviceProvider
            .GetRequiredService<IPayVelixClientFactory>()
            .CreateClient("merchant-key");

        await client.Payments.CreateAsync(CreateRequest(), "idem-headers");

        var request = Assert.Single(handler.Requests);
        Assert.StartsWith("PayVelix.DotNetSdk/", request.GetHeader("User-Agent"));
        Assert.False(string.IsNullOrWhiteSpace(request.GetHeader("X-SDK-Version")));
        Assert.Equal("application/json", request.GetHeader("Accept"));
    }

    private static ServiceProvider CreateServiceProvider(
        RecordingHttpMessageHandler handler,
        Action<Options.PayVelixOptions> configureOptions)
    {
        var services = new ServiceCollection();
        services.AddPayVelix(configureOptions);
        services
            .AddHttpClient(PayVelixHttp.HttpClientName)
            .ConfigurePrimaryHttpMessageHandler(() => handler);

        return services.BuildServiceProvider();
    }

    private static CreatePaymentRequest CreateRequest()
    {
        return new CreatePaymentRequest
        {
            Amount = 1,
            ReturnUrl = "https://example.com/return",
            WebhookUrl = "https://example.com/webhook"
        };
    }

    private static HttpClient GetHttpClient(IPayVelixPaymentsClient paymentsClient)
    {
        var field = paymentsClient.GetType().GetField(
            "_httpClient",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);

        return Assert.IsType<HttpClient>(field?.GetValue(paymentsClient));
    }

    private static HttpResponseMessage SuccessPaymentResponse()
    {
        return new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = JsonContent(
                """
                {
                    "paymentId": "123e4567-e89b-12d3-a456-426614174000",
                    "amount": 1,
                    "paymentLink": "https://checkout.payvelix.com/123e4567-e89b-12d3-a456-426614174000",
                    "expiresAt": "2026-07-04T10:50:05.092Z"
                }
                """)
        };
    }

    private static HttpResponseMessage SuccessVerifyResponse()
    {
        return new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = JsonContent(
                """
                {
                    "paymentId": "123e4567-e89b-12d3-a456-426614174000",
                    "amount": 1,
                    "paidAmount": 1,
                    "feeAmount": 0,
                    "expectedAmount": 1,
                    "merchantReceivableAmount": 1,
                    "currency": "USDT",
                    "network": "TRC20",
                    "status": "Paid",
                    "expiresAt": "2026-07-04T10:50:05.092Z"
                }
                """)
        };
    }

    private static HttpResponseMessage SuccessBalanceResponse()
    {
        return new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = JsonContent("""{"currencies":{"USD":10},"usdEquivalent":10}""")
        };
    }

    private static StringContent JsonContent(string json)
    {
        return new StringContent(json, System.Text.Encoding.UTF8, "application/json");
    }

    private sealed class RecordingHttpMessageHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> _responseFactory;
        private readonly ConcurrentBag<RecordedRequest> _requests = new();

        public RecordingHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> responseFactory)
            : this((request, _) => Task.FromResult(responseFactory(request)))
        {
        }

        public RecordingHttpMessageHandler(Func<HttpRequestMessage, Task<HttpResponseMessage>> responseFactory)
            : this((request, _) => responseFactory(request))
        {
        }

        public RecordingHttpMessageHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> responseFactory)
        {
            _responseFactory = responseFactory;
        }

        public IReadOnlyCollection<RecordedRequest> Requests => _requests.ToArray();

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            _requests.Add(RecordedRequest.From(request));
            return await _responseFactory(request, cancellationToken);
        }
    }

    private sealed record RecordedRequest(
        HttpMethod Method,
        string PathAndQuery,
        IReadOnlyDictionary<string, string> Headers,
        string? DefaultApiKeyHeader)
    {
        public string? GetHeader(string name)
        {
            return Headers.TryGetValue(name, out var value) ? value : null;
        }

        public static RecordedRequest From(HttpRequestMessage request)
        {
            var headers = request.Headers.ToDictionary(
                header => header.Key,
                header => string.Join(",", header.Value),
                StringComparer.OrdinalIgnoreCase);

            return new RecordedRequest(
                request.Method,
                request.RequestUri?.PathAndQuery ?? string.Empty,
                headers,
                request.Headers.Contains("X-Api-Key") ? null : request.Headers.ToString());
        }
    }
}
