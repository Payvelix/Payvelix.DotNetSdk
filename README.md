# PayVelix Client
[![Build](https://github.com/Payvelix/Payvelix.DotNetSdk/actions/workflows/build.yml/badge.svg)](https://github.com/Payvelix/Payvelix.DotNetSdk/actions/workflows/build.yml)
[![PayVelix NuGet](https://img.shields.io/nuget/v/PayVelix.svg?label=PayVelix)](https://www.nuget.org/packages/PayVelix)
[![PayVelix.Contracts NuGet](https://img.shields.io/nuget/v/PayVelix.Contracts.svg?label=PayVelix.Contracts)](https://www.nuget.org/packages/PayVelix.Contracts)

PayVelix Client is a .NET 8 SDK for integrating applications with the PayVelix payment API. It provides typed clients for payments and balances, works with `IHttpClientFactory`, and supports both single-tenant and multi-tenant applications.

## Installation

```powershell
dotnet add package PayVelix --version 1.1.0
```

The shared contract package is published separately for applications that only need DTOs and exceptions:

```powershell
dotnet add package PayVelix.Contracts --version 1.1.0
```

Requirements:

- .NET SDK 8.0 or later
- A valid PayVelix merchant API key

## Single-Tenant Configuration

Use the standard registration when the application uses one PayVelix merchant account:

```csharp
using PayVelix.DependencyInjection;

builder.Services.AddPayVelix(options =>
{
    options.ApiKey = builder.Configuration["PayVelix:ApiKey"]
        ?? throw new InvalidOperationException("PayVelix API key is missing.");
    options.BaseUrl = "https://api.payvelix.com";
    options.Timeout = TimeSpan.FromSeconds(30);
});
```

Inject `IPayVelixClient` as before:

```csharp
using PayVelix;
using PayVelix.Contracts.Payments;

public sealed class PaymentService(IPayVelixClient payVelix)
{
    public Task<CreatePaymentResponse> CreateAsync(
        CreatePaymentRequest request,
        CancellationToken cancellationToken)
    {
        return payVelix.Payments.CreateAsync(request, cancellationToken);
    }
}
```

## Multi-Tenant Configuration

`AddPayVelix` also registers `IPayVelixClientFactory`. Use it when each merchant has a different API key loaded dynamically from a database, vault, or secret store.

Configure shared defaults once:

```csharp
builder.Services.AddPayVelix(options =>
{
    options.ApiKey = builder.Configuration["PayVelix:FallbackApiKey"]!;
    options.BaseUrl = "https://api.payvelix.com";
    options.Timeout = TimeSpan.FromSeconds(30);
});
```

Do not place every merchant API key in `appsettings.json`. Store merchant keys in your platform database or secret store, retrieve the key for the current merchant, and pass it to the factory.

## Creating A Client For A Merchant

```csharp
using PayVelix;
using PayVelix.Contracts.Payments;

public interface IMerchantSecretStore
{
    Task<string> GetPayVelixApiKeyAsync(string merchantId, CancellationToken cancellationToken);
}

public sealed class MerchantPaymentGateway(
    IPayVelixClientFactory payVelixClientFactory,
    IMerchantSecretStore merchantSecretStore)
{
    public async Task<CreatePaymentResponse> CreatePaymentAsync(
        string merchantId,
        CreatePaymentRequest request,
        CancellationToken cancellationToken)
    {
        var merchantApiKey = await merchantSecretStore.GetPayVelixApiKeyAsync(
            merchantId,
            cancellationToken);

        var payVelix = payVelixClientFactory.CreateClient(merchantApiKey);

        return await payVelix.Payments.CreateAsync(request, cancellationToken);
    }
}
```

Advanced callers can override base URL or timeout per created client:

```csharp
var payVelix = payVelixClientFactory.CreateClient(new PayVelixClientConfiguration
{
    ApiKey = merchantApiKey,
    BaseUrl = "https://sandbox-api.example.com",
    Timeout = TimeSpan.FromSeconds(15)
});
```

The factory validates that API keys are not blank, base URLs are absolute HTTP or HTTPS URLs, and timeout values are greater than zero.

## Creating Payments

```csharp
using PayVelix.Contracts.Payments;

var payment = await payVelix.Payments.CreateAsync(
    new CreatePaymentRequest
    {
        IdempotencyKey = orderId,
        Amount = 25.50m,
        Currency = "USD",
        ReturnUrl = $"https://example.com/payments/return?orderId={orderId}",
        WebhookUrl = "https://example.com/webhooks/payvelix",
        CallbackParams = new Dictionary<string, string>
        {
            ["orderId"] = orderId
        }
    },
    cancellationToken);
```

`Amount` must be greater than zero. `IdempotencyKey` must be supplied either on the request or through the explicit overload:

```csharp
var payment = await payVelix.Payments.CreateAsync(
    request,
    explicitIdempotencyKey,
    cancellationToken);
```

The explicit idempotency key overload takes precedence. The SDK sends the idempotency key through the `Idempotency-Key` HTTP header, not in the JSON body.

## Persisting `PaymentId` And `PaymentLink`

`CreatePaymentResponse` includes:

| Property | Purpose |
| --- | --- |
| `PaymentId` | Permanent PayVelix provider transaction reference. Persist this in your order or transaction table. |
| `PaymentLink` | Redirect URL for the customer's checkout. Persist it if your application needs to resume an incomplete checkout. |
| `ExpiresAt` | Indicates how long the payment link remains usable. |

The verify endpoint does not reconstruct or return the original payment link unless the PayVelix API itself supports that behavior. Do not assume `PaymentLink` can always be rebuilt from `PaymentId`.

## Return URL Versus Webhook

`ReturnUrl` and `WebhookUrl` serve different roles:

| Field | Purpose |
| --- | --- |
| `ReturnUrl` | Browser redirect URL used after the customer completes or leaves checkout. |
| `WebhookUrl` | Server-to-server notification endpoint used by PayVelix to notify your application. |

Always verify the payment through the PayVelix verify endpoint before marking an order as paid. Browser redirects and webhooks may arrive multiple times or in any order, so webhook processing must be idempotent.

The SDK does not implement webhook signature verification because no PayVelix webhook signature mechanism is defined in the current API surface.

## Verifying Payments

```csharp
using PayVelix.Contracts.Payments;

var payment = await payVelix.Payments.VerifyAsync(paymentId, cancellationToken);

if (payment.Status == VerifyPaymentStatus.Paid)
{
    // Mark the order as paid.
}
```

Current `VerifyPaymentStatus` values:

| Status | General handling |
| --- | --- |
| `Pending` | Payment is still pending or processing. Wait and verify again later. |
| `Paid` | Payment completed successfully. |
| `Mismatch` | Payment requires manual review because the paid amount or expected amount does not match. |
| `Expired` | Payment link or payment attempt expired. Treat as terminal unless a new payment is created. |
| `Cancelled` | Payment was cancelled. Treat as terminal unless a new payment is created. |

The SDK returns the typed status and does not collapse non-paid states into a generic failure. Your application should decide whether to wait, retry, reject, cancel, or manually review the transaction.

## Idempotency

Payment creation is a financial operation and should be idempotent. Use a stable idempotency key for the same logical payment attempt, such as your order ID or payment attempt ID. Reuse the same key across retries so the PayVelix API can return the same payment instead of creating duplicates.

## Error Handling

For non-successful PayVelix API responses, the SDK throws `PayVelixApiException`:

```csharp
using PayVelix.Contracts.Common;

try
{
    var payment = await payVelix.Payments.VerifyAsync(paymentId, cancellationToken);
}
catch (PayVelixApiException ex)
{
    logger.LogWarning(
        "PayVelix request failed. StatusCode: {StatusCode}, ErrorCode: {ErrorCode}",
        ex.StatusCode,
        ex.ErrorCode);
}
```

Useful exception properties:

| Property | Description |
| --- | --- |
| `StatusCode` | HTTP status code returned by the API. |
| `ErrorCode` | PayVelix error code, when available. |
| `ResponseBody` | Raw response body for troubleshooting. The SDK redacts the request API key if it appears in the body. |

Failure categories:

- Invalid SDK arguments throw `ArgumentException`, `ArgumentNullException`, or `ArgumentOutOfRangeException`.
- HTTP and network failures are surfaced by `HttpClient` exceptions.
- PayVelix API errors throw `PayVelixApiException`.
- Successful HTTP responses with empty, invalid, or malformed payloads throw `PayVelixApiException`.
- Cancellation requested through the supplied cancellation token remains `OperationCanceledException` or `TaskCanceledException` and is not converted to `PayVelixApiException`.

## Security Considerations

- Do not log, serialize, or expose merchant API keys.
- Multi-tenant clients add `X-Api-Key` directly to each outgoing request instead of shared `HttpClient.DefaultRequestHeaders`.
- Factory-created clients are lightweight; `IHttpClientFactory` manages the underlying handlers.
- Do not cache clients by raw API key unless your application has a specific need and the cache cannot expose or retain secrets unnecessarily.
- Store merchant API keys in a database or secret store designed for sensitive values, not in application configuration files.

## Get Balance

```csharp
var balance = await payVelix.Balance.GetAsync(cancellationToken: cancellationToken);
```

When `id` is null or empty, the SDK calls `/api/Balance`. When an `id` is provided, it calls `/api/Balance?id={value}` with the value URL-escaped before being sent.

## Models

### `CreatePaymentRequest`

| Property | Type | Description |
| --- | --- | --- |
| `IdempotencyKey` | `string?` | Required for create-payment calls unless passed to the explicit overload. Sent as the `Idempotency-Key` header, not in the JSON body. |
| `ReturnUrl` | `string?` | URL where the customer is redirected after payment. |
| `Amount` | `decimal` | Payment amount. Must be greater than zero. |
| `Currency` | `string` | Currency code. Defaults to `USDT`. |
| `WebhookUrl` | `string?` | URL that receives PayVelix webhook callbacks. |
| `CallbackParams` | `Dictionary<string, string>?` | Custom parameters for tracking orders or callback context. |

### `CreatePaymentResponse`

| Property | Type |
| --- | --- |
| `PaymentId` | `Guid` |
| `Amount` | `decimal` |
| `PaymentLink` | `string` |
| `ExpiresAt` | `DateTimeOffset` |
| `AdditionalData` | `Dictionary<string, JsonElement>?` |

### `VerifyPaymentResponse`

| Property | Type |
| --- | --- |
| `PaymentId` | `Guid` |
| `Amount` | `decimal` |
| `PaidAmount` | `decimal` |
| `FeeAmount` | `decimal` |
| `ExpectedAmount` | `decimal` |
| `MerchantReceivableAmount` | `decimal` |
| `Currency` | `string?` |
| `Network` | `string?` |
| `Status` | `VerifyPaymentStatus` |
| `ExpiresAt` | `DateTimeOffset` |
| `AdditionalData` | `Dictionary<string, JsonElement>?` |

### `BalanceResponse`

| Property | Type |
| --- | --- |
| `Currencies` | `Dictionary<string, decimal>?` |
| `UsdEquivalent` | `decimal` |
| `AdditionalData` | `Dictionary<string, JsonElement>?` |

## Build And Test

```powershell
dotnet restore .\PayVelix.sln
dotnet build .\PayVelix.sln --configuration Release
dotnet test .\PayVelix.sln --configuration Release
```

## Package Release

Package versions are controlled by Git tags. Publishing a GitHub release or pushing a tag named `v1.1.0` publishes NuGet packages with version `1.1.0`.

The publish workflow restores, builds, tests, packs `PayVelix.Contracts` before `PayVelix`, publishes symbols packages, and uses NuGet.org Trusted Publishing through GitHub OIDC.
