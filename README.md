# PayVelix Client
[![Build](https://github.com/Payvelix/Payvelix.DotNetSdk/actions/workflows/build.yml/badge.svg)](https://github.com/Payvelix/Payvelix.DotNetSdk/actions/workflows/build.yml)
[![PayVelix NuGet](https://img.shields.io/nuget/v/PayVelix.svg?label=PayVelix)](https://www.nuget.org/packages/PayVelix)
[![PayVelix.Contracts NuGet](https://img.shields.io/nuget/v/PayVelix.Contracts.svg?label=PayVelix.Contracts)](https://www.nuget.org/packages/PayVelix.Contracts)

PayVelix Client is a .NET 8 SDK for integrating applications with the PayVelix payment API. It provides a small typed wrapper around the payment endpoints and is designed to work with `HttpClientFactory` and Microsoft Dependency Injection.

## Features

- Create payments through `/api/Payments/Create`
- Verify payments through `/api/Payments/{paymentId}/Verify`
- Get balance through `/api/Balance`
- Send the idempotency key for payment creation with the `Idempotency-Key` header
- Send the API key with the `X-Api-Key` header
- Serialize and deserialize JSON with camelCase property names
- Preserve unknown response fields in `AdditionalData`
- Convert unsuccessful API responses to `PayVelixApiException`

## Project Structure

```text
PayVelix.Client/
|- PayVelix/              # Main client, DI setup, and HTTP implementation
|- PayVelix.Contracts/    # DTOs, enums, and shared exceptions
|- PayVelix.Tests/        # xUnit tests
`- README.md
```

## Requirements

- .NET SDK 8.0 or later
- A valid PayVelix API key

## Installation

Install the SDK package from NuGet:

```powershell
dotnet add package PayVelix --version 1.0.0
```

The shared contract package is published separately for advanced scenarios where applications only need DTOs and exceptions:

```powershell
dotnet add package PayVelix.Contracts --version 1.0.0
```

During local SDK development, reference the projects from your consuming application:

```powershell
dotnet add <YourProject>.csproj reference .\PayVelix\PayVelix.csproj
dotnet add <YourProject>.csproj reference .\PayVelix.Contracts\PayVelix.Contracts.csproj
```

## Configuration

In ASP.NET Core or any application that uses `IServiceCollection`:

```csharp
using PayVelix.DependencyInjection;

builder.Services.AddPayVelix(options =>
{
    options.ApiKey = builder.Configuration["PayVelix:ApiKey"]
        ?? throw new InvalidOperationException("PayVelix API key is missing.");

    options.BaseUrl = builder.Configuration["PayVelix:BaseUrl"]
        ?? "https://api.payvelix.com";

    options.Timeout = TimeSpan.FromSeconds(30);
});
```

Available options:

| Option | Default | Description |
| --- | --- | --- |
| `ApiKey` | `string.Empty` | PayVelix API key. An empty value causes client creation to fail. |
| `BaseUrl` | `https://api.payvelix.com` | Base URL of the PayVelix API. |
| `Timeout` | `30s` | HTTP request timeout. |

Example `appsettings.json`:

```json
{
  "PayVelix": {
    "ApiKey": "your_api_key",
    "BaseUrl": "https://api.payvelix.com"
  }
}
```

## SDK Usage

Inject `IPayVelixClient` into your service after registering `AddPayVelix`. Payment operations are exposed through `payVelix.Payments`, and balance operations are exposed through `payVelix.Balance`.

| Method | Purpose |
| --- | --- |
| `CreateAsync(CreatePaymentRequest request, CancellationToken cancellationToken = default)` | Creates a payment using `request.IdempotencyKey` and returns the payment identifier, amount, payment link, and expiration time. |
| `CreateAsync(CreatePaymentRequest request, string idempotencyKey, CancellationToken cancellationToken = default)` | Creates a payment using an explicit idempotency key. |
| `VerifyAsync(string paymentId, CancellationToken cancellationToken = default)` | Verifies an existing payment and returns its current status, paid amount, fees, currency, and network details. |
| `VerifyAsync(Guid paymentId, CancellationToken cancellationToken = default)` | Verifies an existing payment by GUID. |
| `GetAsync(string? id = null, CancellationToken cancellationToken = default)` | Gets balances by currency and the total USD equivalent through `/api/Balance`. |

## Create a Payment

Use `CreateAsync` when your application needs to start a new payment.

```csharp
Task<CreatePaymentResponse> CreateAsync(
    CreatePaymentRequest request,
    CancellationToken cancellationToken = default);

Task<CreatePaymentResponse> CreateAsync(
    CreatePaymentRequest request,
    string idempotencyKey,
    CancellationToken cancellationToken = default);
```

```csharp
using PayVelix;
using PayVelix.Contracts.Payments;

public sealed class CheckoutService
{
    private readonly IPayVelixClient _payVelix;

    public CheckoutService(IPayVelixClient payVelix)
    {
        _payVelix = payVelix;
    }

    public async Task<CreatePaymentResponse> CreatePaymentAsync(
        string orderId,
        CancellationToken cancellationToken = default)
    {
        return await _payVelix.Payments.CreateAsync(
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
    }
}
```

`Amount` must be greater than zero. Otherwise, `ArgumentException` is thrown.
`IdempotencyKey` must be provided either on the request or through the explicit overload. Reusing the same key for a retried create-payment request returns the same payment instead of creating a duplicate.

Store the returned `PaymentId` in your own order or transaction record. If the API returns a `PaymentLink`, redirect the customer to that URL to complete the payment.

## Verify a Payment

Use `VerifyAsync` after a redirect, webhook, or background reconciliation job to fetch the latest payment state.

```csharp
Task<VerifyPaymentResponse> VerifyAsync(
    string paymentId,
    CancellationToken cancellationToken = default);
```

```csharp
using PayVelix;
using PayVelix.Contracts.Payments;

public sealed class PaymentVerificationService
{
    private readonly IPayVelixClient _payVelix;

    public PaymentVerificationService(IPayVelixClient payVelix)
    {
        _payVelix = payVelix;
    }

    public async Task<bool> IsPaidAsync(
        string paymentId,
        CancellationToken cancellationToken = default)
    {
        var payment = await _payVelix.Payments.VerifyAsync(
            paymentId,
            cancellationToken);

        return payment.Status == VerifyPaymentStatus.Paid;
    }
}
```

`paymentId` must not be null, empty, or whitespace. Otherwise, `ArgumentException` is thrown.

Compare `VerifyPaymentResponse.Status` with `VerifyPaymentStatus.Paid` when you need to confirm that the payment has been completed.

## Get Balance

Use `GetAsync` when your application needs to read the current balance.

```csharp
Task<BalanceResponse> GetAsync(
    string? id = null,
    CancellationToken cancellationToken = default);
```

```csharp
using PayVelix;
using PayVelix.Contracts.Balance;

public sealed class BalanceService
{
    private readonly IPayVelixClient _payVelix;

    public BalanceService(IPayVelixClient payVelix)
    {
        _payVelix = payVelix;
    }

    public async Task<BalanceResponse> GetBalanceAsync(
        string? id = null,
        CancellationToken cancellationToken = default)
    {
        return await _payVelix.Balance.GetAsync(id, cancellationToken);
    }
}
```

When `id` is null or empty, the SDK calls `/api/Balance`. When an `id` is provided, it calls `/api/Balance?id={value}` with the value URL-escaped before being sent.

Successful responses are deserialized into `BalanceResponse`:

```json
{
  "currencies": {
    "BTC": 0.1,
    "USDT": 25.5
  },
  "usdEquivalent": 1025.5
}
```

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

## Error Handling

For unsuccessful HTTP responses, the client throws `PayVelixApiException`:

```csharp
using PayVelix.Contracts.Common;

try
{
    var payment = await payVelix.Payments.VerifyAsync(paymentId, cancellationToken);
}
catch (PayVelixApiException ex)
{
    logger.LogWarning(
        "PayVelix request failed. StatusCode: {StatusCode}, ErrorCode: {ErrorCode}, Body: {Body}",
        ex.StatusCode,
        ex.ErrorCode,
        ex.ResponseBody);
}
```

Useful exception properties:

| Property | Description |
| --- | --- |
| `StatusCode` | HTTP status code returned by the API. |
| `ErrorCode` | PayVelix error code, when available. |
| `ResponseBody` | Raw response body for troubleshooting. |

The client supports both direct error responses such as `{"code":"...","message":"..."}` and nested error responses such as `{"error":{...}}`.

## Build and Test

```powershell
dotnet restore .\PayVelix.sln
dotnet build .\PayVelix.sln
dotnet test .\PayVelix.sln
```

## Package Release

Package versions are controlled by Git tags. Publishing a GitHub release or pushing a tag named `v1.2.3` publishes NuGet packages with version `1.2.3`.

The publish workflow:

- Restores, builds, and tests `PayVelix.sln` in Release mode.
- Passes `-p:Version=$PKG_VERSION` to build and pack so package versions always match the tag.
- Packs `PayVelix.Contracts` and `PayVelix`, including symbols packages.
- Publishes `PayVelix.Contracts` before `PayVelix` so the SDK dependency is available first.
- Uses NuGet.org Trusted Publishing through GitHub OIDC, not a `NUGET_API_KEY` repository secret.

Before the first release, configure NuGet.org Trusted Publishing for repository `Payvelix/Payvelix.DotNetSdk`, workflow file `publish-nuget.yml`, and package owner `Payvelix`.

## Implementation Notes

- `IPayVelixClient` is registered as scoped.
- `IPayVelixBalanceClient` is registered with `AddHttpClient`.
- `IPayVelixPaymentsClient` is registered with `AddHttpClient`.
- `Accept: application/json` is added automatically.
- `User-Agent: PayVelix.DotNetSdk/{version}` and `X-SDK-Version: {version}` are added automatically.
- The SDK version is read from the assembly informational version, falling back to the assembly version.
- `X-Api-Key` is built from `PayVelixOptions.ApiKey`.
- `Idempotency-Key` is required for payment creation.
- Empty or invalid successful API responses are converted to `PayVelixApiException`.

