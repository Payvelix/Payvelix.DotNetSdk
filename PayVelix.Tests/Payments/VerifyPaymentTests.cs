using System.Net;
using PayVelix.Contracts.Common;
using PayVelix.Contracts.Payments;
using PayVelix.Payments;

namespace PayVelix.Tests.Payments;

public sealed class VerifyPaymentTests
{
    [Fact]
    public async Task VerifyAsync_SendsGetToVerifyPaymentEndpoint()
    {
        const string paymentId = "123e4567-e89b-12d3-a456-426614174000";
        using var handler = new FakeHttpMessageHandler(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = JsonContent(SuccessJson(paymentId))
        });
        var client = CreateClient(handler);

        await client.VerifyAsync(paymentId);

        Assert.Equal(HttpMethod.Get, handler.Request?.Method);
        Assert.Equal($"/api/Payments/{paymentId}/Verify", handler.Request?.RequestUri?.PathAndQuery);
    }

    [Fact]
    public async Task VerifyAsync_GuidOverloadSendsGetToVerifyPaymentEndpoint()
    {
        var paymentId = Guid.Parse("123e4567-e89b-12d3-a456-426614174000");
        using var handler = new FakeHttpMessageHandler(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = JsonContent(SuccessJson(paymentId.ToString("D")))
        });
        var client = CreateClient(handler);

        await client.VerifyAsync(paymentId);

        Assert.Equal($"/api/Payments/{paymentId:D}/Verify", handler.Request?.RequestUri?.PathAndQuery);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task VerifyAsync_ThrowsArgumentExceptionWhenPaymentIdIsMissing(string? paymentId)
    {
        using var handler = new FakeHttpMessageHandler(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = JsonContent(SuccessJson("123e4567-e89b-12d3-a456-426614174000"))
        });
        var client = CreateClient(handler);

        await Assert.ThrowsAsync<ArgumentException>(() =>
            client.VerifyAsync(paymentId!));
    }

    [Fact]
    public async Task VerifyAsync_ThrowsArgumentExceptionWhenPaymentIdIsNotGuid()
    {
        using var handler = new FakeHttpMessageHandler(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = JsonContent(SuccessJson("123e4567-e89b-12d3-a456-426614174000"))
        });
        var client = CreateClient(handler);

        await Assert.ThrowsAsync<ArgumentException>(() =>
            client.VerifyAsync("not-a-guid"));
    }

    [Fact]
    public async Task VerifyAsync_DeserializesSuccessfulResponse()
    {
        const string paymentId = "123e4567-e89b-12d3-a456-426614174000";
        using var handler = new FakeHttpMessageHandler(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = JsonContent(
                """
                {
                    "paymentId": "123e4567-e89b-12d3-a456-426614174000",
                    "amount": 1,
                    "paidAmount": 1,
                    "feeAmount": 1,
                    "expectedAmount": 1,
                    "merchantReceivableAmount": 1,
                    "currency": null,
                    "network": null,
                    "status": "Pending",
                    "expiresAt": "2026-07-02T12:06:36.695Z",
                    "providerTraceId": "trace_789"
                }
                """)
        });
        var client = CreateClient(handler);

        var response = await client.VerifyAsync(paymentId);

        Assert.Equal(Guid.Parse(paymentId), response.PaymentId);
        Assert.Equal(1, response.Amount);
        Assert.Equal(1, response.PaidAmount);
        Assert.Equal(1, response.FeeAmount);
        Assert.Equal(1, response.ExpectedAmount);
        Assert.Equal(1, response.MerchantReceivableAmount);
        Assert.Null(response.Currency);
        Assert.Null(response.Network);
        Assert.Equal(VerifyPaymentStatus.Pending, response.Status);
        Assert.Equal(DateTimeOffset.Parse("2026-07-02T12:06:36.695Z"), response.ExpiresAt);
        Assert.NotNull(response.AdditionalData);
        Assert.True(response.AdditionalData.ContainsKey("providerTraceId"));
    }

    [Fact]
    public async Task VerifyAsync_ThrowsPayVelixApiExceptionOnUnauthorized()
    {
        using var handler = new FakeHttpMessageHandler(new HttpResponseMessage(HttpStatusCode.Unauthorized)
        {
            Content = JsonContent(ErrorJson("unauthorized", "Invalid API key."))
        });
        var client = CreateClient(handler);

        var exception = await Assert.ThrowsAsync<PayVelixApiException>(() =>
            client.VerifyAsync(Guid.NewGuid()));

        Assert.Equal(HttpStatusCode.Unauthorized, exception.StatusCode);
    }

    [Fact]
    public async Task VerifyAsync_ThrowsPayVelixApiExceptionOnNotFound()
    {
        using var handler = new FakeHttpMessageHandler(new HttpResponseMessage(HttpStatusCode.NotFound)
        {
            Content = JsonContent(ErrorJson("not_found", "Payment was not found."))
        });
        var client = CreateClient(handler);

        var exception = await Assert.ThrowsAsync<PayVelixApiException>(() =>
            client.VerifyAsync(Guid.NewGuid()));

        Assert.Equal(HttpStatusCode.NotFound, exception.StatusCode);
    }

    [Fact]
    public async Task VerifyAsync_PreservesRawResponseBodyInApiException()
    {
        const string responseBody = "not-json";
        using var handler = new FakeHttpMessageHandler(new HttpResponseMessage(HttpStatusCode.NotFound)
        {
            Content = JsonContent(responseBody)
        });
        var client = CreateClient(handler);

        var exception = await Assert.ThrowsAsync<PayVelixApiException>(() =>
            client.VerifyAsync(Guid.NewGuid()));

        Assert.Equal(responseBody, exception.ResponseBody);
    }

    [Fact]
    public async Task VerifyAsync_UsesNestedErrorCodeInApiException()
    {
        using var handler = new FakeHttpMessageHandler(new HttpResponseMessage(HttpStatusCode.NotFound)
        {
            Content = JsonContent(ErrorJson("not_found", "Payment was not found."))
        });
        var client = CreateClient(handler);

        var exception = await Assert.ThrowsAsync<PayVelixApiException>(() =>
            client.VerifyAsync(Guid.NewGuid()));

        Assert.Equal("not_found", exception.ErrorCode);
    }

    [Fact]
    public async Task VerifyAsync_UsesNestedErrorMessageInApiException()
    {
        using var handler = new FakeHttpMessageHandler(new HttpResponseMessage(HttpStatusCode.NotFound)
        {
            Content = JsonContent(ErrorJson("not_found", "Payment was not found."))
        });
        var client = CreateClient(handler);

        var exception = await Assert.ThrowsAsync<PayVelixApiException>(() =>
            client.VerifyAsync(Guid.NewGuid()));

        Assert.Equal("Payment was not found.", exception.Message);
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

    private static string SuccessJson(string paymentId)
    {
        return $$"""
        {
            "paymentId": "{{paymentId}}",
            "amount": 1,
            "paidAmount": 1,
            "feeAmount": 1,
            "expectedAmount": 1,
            "merchantReceivableAmount": 1,
            "currency": null,
            "network": null,
            "status": "Pending",
            "expiresAt": "2026-07-02T12:06:36.695Z"
        }
        """;
    }

    private static string ErrorJson(string code, string message)
    {
        return $$"""
        {
            "error": {
                "code": "{{code}}",
                "message": "{{message}}",
                "requestId": "req_123",
                "details": null
            }
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

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            Request = request;
            return Task.FromResult(_response);
        }
    }
}
