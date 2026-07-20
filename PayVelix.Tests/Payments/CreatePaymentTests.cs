using System.Net;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using PayVelix.Contracts.Common;
using PayVelix.Contracts.Payments;
using PayVelix.DependencyInjection;
using PayVelix.Payments;

namespace PayVelix.Tests.Payments;

public sealed class CreatePaymentTests
{
    [Fact]
    public async Task CreateAsync_SendsPostToCreatePaymentEndpoint()
    {
        using var handler = new FakeHttpMessageHandler(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = JsonContent(SuccessJson())
        });
        var client = CreateClient(handler);

        await client.CreateAsync(new CreatePaymentRequest { Amount = 1 }, "order_123");

        Assert.Equal(HttpMethod.Post, handler.Request?.Method);
        Assert.Equal("/api/Payments/Create", handler.Request?.RequestUri?.PathAndQuery);
    }

    [Fact]
    public async Task CreateAsync_SerializesRequestAsCamelCaseJson()
    {
        using var handler = new FakeHttpMessageHandler(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = JsonContent(SuccessJson())
        });
        var client = CreateClient(handler);

        await client.CreateAsync(new CreatePaymentRequest
        {
            ReturnUrl = "https://example.com/return",
            Amount = 1,
            Currency = "USD",
            WebhookUrl = "https://example.com/webhook",
            CallbackParams = new Dictionary<string, string>
            {
                ["orderId"] = "order_1042"
            }
        }, "order_1042");

        using var document = JsonDocument.Parse(handler.RequestBody);
        var root = document.RootElement;

        Assert.True(root.TryGetProperty("returnUrl", out _));
        Assert.True(root.TryGetProperty("amount", out _));
        Assert.True(root.TryGetProperty("currency", out _));
        Assert.True(root.TryGetProperty("webhookUrl", out _));
        Assert.True(root.TryGetProperty("callbackParams", out var callbackParams));
        Assert.True(callbackParams.TryGetProperty("orderId", out _));
        Assert.False(root.TryGetProperty("ReturnUrl", out _));
        Assert.False(root.TryGetProperty("idempotencyKey", out _));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task CreateAsync_ThrowsArgumentExceptionWhenAmountIsNotPositive(decimal amount)
    {
        using var handler = new FakeHttpMessageHandler(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = JsonContent(SuccessJson())
        });
        var client = CreateClient(handler);

        await Assert.ThrowsAsync<ArgumentException>(() =>
            client.CreateAsync(new CreatePaymentRequest { Amount = amount }));
    }

    [Fact]
    public async Task CreateAsync_ThrowsArgumentExceptionWhenIdempotencyKeyIsMissing()
    {
        using var handler = new FakeHttpMessageHandler(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = JsonContent("""{"paymentId":"123e4567-e89b-12d3-a456-426614174000"}""")
        });
        var client = CreateClient(handler);

        await Assert.ThrowsAsync<ArgumentException>(() =>
            client.CreateAsync(new CreatePaymentRequest { Amount = 1 }));
    }

    [Fact]
    public async Task CreateAsync_DeserializesSuccessfulResponse()
    {
        using var handler = new FakeHttpMessageHandler(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = JsonContent(
                """
                {
                    "paymentId": "123e4567-e89b-12d3-a456-426614174000",
                    "amount": 1,
                    "paymentLink": "https://checkout.payvelix.com/123e4567-e89b-12d3-a456-426614174000",
                    "expiresAt": "2026-07-04T10:50:05.092Z",
                    "providerTraceId": "trace_456"
                }
                """)
        });
        var client = CreateClient(handler);

        var response = await client.CreateAsync(new CreatePaymentRequest { Amount = 1 }, "order_123");

        Assert.Equal(Guid.Parse("123e4567-e89b-12d3-a456-426614174000"), response.PaymentId);
        Assert.Equal(1, response.Amount);
        Assert.Equal("https://checkout.payvelix.com/123e4567-e89b-12d3-a456-426614174000", response.PaymentLink);
        Assert.Equal(DateTimeOffset.Parse("2026-07-04T10:50:05.092Z"), response.ExpiresAt);
        Assert.NotNull(response.AdditionalData);
        Assert.True(response.AdditionalData.ContainsKey("providerTraceId"));
    }

    [Fact]
    public async Task CreateAsync_ThrowsPayVelixApiExceptionOnNonSuccessStatus()
    {
        using var handler = new FakeHttpMessageHandler(new HttpResponseMessage(HttpStatusCode.BadRequest)
        {
            Content = JsonContent("""{"code":"invalid_amount","message":"Invalid amount."}""")
        });
        var client = CreateClient(handler);

        var exception = await Assert.ThrowsAsync<PayVelixApiException>(() =>
            client.CreateAsync(new CreatePaymentRequest { Amount = 1 }, "order_123"));

        Assert.Equal(HttpStatusCode.BadRequest, exception.StatusCode);
        Assert.Equal("invalid_amount", exception.ErrorCode);
        Assert.Equal("Invalid amount.", exception.Message);
    }

    [Fact]
    public async Task CreateAsync_PreservesRawResponseBodyInApiException()
    {
        const string responseBody = "not-json";
        using var handler = new FakeHttpMessageHandler(new HttpResponseMessage(HttpStatusCode.InternalServerError)
        {
            Content = JsonContent(responseBody)
        });
        var client = CreateClient(handler);

        var exception = await Assert.ThrowsAsync<PayVelixApiException>(() =>
            client.CreateAsync(new CreatePaymentRequest { Amount = 1 }, "order_123"));

        Assert.Equal(responseBody, exception.ResponseBody);
    }

    [Fact]
    public async Task AddPayVelix_SendsApiKeyHeader()
    {
        using var handler = new FakeHttpMessageHandler(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = JsonContent(SuccessJson())
        });
        var services = new ServiceCollection();

        services.AddPayVelix(options =>
        {
            options.ApiKey = "pv_sandbox_test_key";
        });
        services
            .AddHttpClient<IPayVelixPaymentsClient, PayVelixPaymentsClient>()
            .ConfigurePrimaryHttpMessageHandler(() => handler);

        using var serviceProvider = services.BuildServiceProvider();
        var payVelix = serviceProvider.GetRequiredService<IPayVelixClient>();

        await payVelix.Payments.CreateAsync(new CreatePaymentRequest { Amount = 1 }, "order_123");

        Assert.NotNull(handler.Request);
        Assert.True(handler.Request.Headers.TryGetValues("X-Api-Key", out var values));
        Assert.Equal("pv_sandbox_test_key", Assert.Single(values));
        Assert.True(handler.Request.Headers.TryGetValues("Idempotency-Key", out var idempotencyValues));
        Assert.Equal("order_123", Assert.Single(idempotencyValues));
    }

    [Fact]
    public async Task CreateAsync_UsesNestedErrorInApiException()
    {
        using var handler = new FakeHttpMessageHandler(new HttpResponseMessage(HttpStatusCode.BadRequest)
        {
            Content = JsonContent(
                """
                {
                    "error": {
                        "code": "invalid_request",
                        "message": "Create payment request is invalid.",
                        "requestId": "req_123",
                        "details": null
                    }
                }
                """)
        });
        var client = CreateClient(handler);

        var exception = await Assert.ThrowsAsync<PayVelixApiException>(() =>
            client.CreateAsync(new CreatePaymentRequest { Amount = 1 }, "order_123"));

        Assert.Equal(HttpStatusCode.BadRequest, exception.StatusCode);
        Assert.Equal("invalid_request", exception.ErrorCode);
        Assert.Equal("Create payment request is invalid.", exception.Message);
    }

    private static IPayVelixPaymentsClient CreateClient(HttpMessageHandler handler)
    {
        var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://api.payvelix.com")
        };

        return new PayVelixPaymentsClient(httpClient);
    }

    private static StringContent JsonContent(string json)
    {
        return new StringContent(json, System.Text.Encoding.UTF8, "application/json");
    }

    private static string SuccessJson()
    {
        return """
        {
            "paymentId": "123e4567-e89b-12d3-a456-426614174000",
            "amount": 1,
            "paymentLink": "https://checkout.payvelix.com/123e4567-e89b-12d3-a456-426614174000",
            "expiresAt": "2026-07-04T10:50:05.092Z"
        }
        """;
    }

    private sealed class FakeHttpMessageHandler : HttpMessageHandler
    {
        private readonly HttpResponseMessage _response;

        public FakeHttpMessageHandler(HttpResponseMessage response)
        {
            _response = response;
        }

        public HttpRequestMessage? Request { get; private set; }

        public string RequestBody { get; private set; } = string.Empty;

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            Request = request;

            if (request.Content is not null)
            {
                RequestBody = await request.Content.ReadAsStringAsync(cancellationToken);
            }

            return _response;
        }
    }
}
