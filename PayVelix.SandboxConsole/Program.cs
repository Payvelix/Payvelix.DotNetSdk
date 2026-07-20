using System.Globalization;
using System.Text;
using Microsoft.Extensions.DependencyInjection;
using PayVelix;
using PayVelix.Contracts.Common;
using PayVelix.Contracts.Payments;
using PayVelix.DependencyInjection;

var options = CommandLineOptions.Parse(args);

if (options.HasFlag("help"))
{
    PrintUsage();
    return;
}

var apiKey = GetRequiredSecret(options, "api-key", "PAYVELIX_API_KEY", "PayVelix sandbox API key");
var baseUrl = GetValue(options, "base-url", "PAYVELIX_BASE_URL", "https://api.payvelix.com");
var amount = GetDecimal(options, "amount", "PAYVELIX_AMOUNT", 5m);
var currency = GetValue(options, "currency", "PAYVELIX_CURRENCY", "USD");
var returnUrl = GetNullableValue(options, "return-url", "PAYVELIX_RETURN_URL", "https://example.com/payvelix/return");
var webhookUrl = GetNullableValue(options, "webhook-url", "PAYVELIX_WEBHOOK_URL", null);
var skipVerify = options.HasFlag("skip-verify");

using var serviceProvider = BuildServiceProvider(apiKey, baseUrl);
var payVelix = serviceProvider.GetRequiredService<IPayVelixClient>();

var orderId = $"manual-{DateTimeOffset.UtcNow:yyyyMMddHHmmss}";

var request = new CreatePaymentRequest
{
    IdempotencyKey = orderId,
    Amount = amount,
    Currency = currency,
    ReturnUrl = returnUrl,
    WebhookUrl = webhookUrl,
    CallbackParams = new Dictionary<string, string>
    {
        ["orderId"] = orderId,
        ["source"] = "PayVelix.SandboxConsole"
    }
};

try
{
    Console.WriteLine("Creating sandbox payment...");
    Console.WriteLine($"Base URL: {baseUrl}");
    Console.WriteLine($"Amount: {amount.ToString(CultureInfo.InvariantCulture)} {currency}");
    Console.WriteLine($"Order ID: {orderId}");
    Console.WriteLine();

    var createResponse = await payVelix.Payments.CreateAsync(request);

    Console.WriteLine("Create payment succeeded.");
    PrintCreateResponse(createResponse);

    if (!skipVerify && createResponse.PaymentId != Guid.Empty)
    {
        Console.WriteLine();
        Console.Write("Verify this payment now? [Y/n]: ");
        var verifyAnswer = Console.ReadLine();

        if (!string.Equals(verifyAnswer, "n", StringComparison.OrdinalIgnoreCase))
        {
            Console.WriteLine();
            Console.WriteLine("Verifying payment...");

            var verifyResponse = await payVelix.Payments.VerifyAsync(createResponse.PaymentId);

            Console.WriteLine("Verify payment succeeded.");
            PrintVerifyResponse(verifyResponse);
        }
    }
}
catch (PayVelixApiException ex)
{
    Console.Error.WriteLine("PayVelix API request failed.");
    Console.Error.WriteLine($"Status: {(int)ex.StatusCode} {ex.StatusCode}");

    if (!string.IsNullOrWhiteSpace(ex.ErrorCode))
    {
        Console.Error.WriteLine($"Error code: {ex.ErrorCode}");
    }

    Console.Error.WriteLine($"Message: {ex.Message}");

    if (!string.IsNullOrWhiteSpace(ex.ResponseBody))
    {
        Console.Error.WriteLine("Response body:");
        Console.Error.WriteLine(ex.ResponseBody);
    }

    Environment.ExitCode = 1;
}
catch (Exception ex)
{
    Console.Error.WriteLine("Sandbox console failed.");
    Console.Error.WriteLine(ex.Message);
    Environment.ExitCode = 1;
}

static ServiceProvider BuildServiceProvider(string apiKey, string baseUrl)
{
    var services = new ServiceCollection();

    services.AddPayVelix(options =>
    {
        options.ApiKey = apiKey;
        options.BaseUrl = baseUrl;
        options.Timeout = TimeSpan.FromSeconds(30);
    });

    return services.BuildServiceProvider();
}

static string GetRequiredSecret(
    CommandLineOptions options,
    string optionName,
    string environmentName,
    string prompt)
{
    var value = options.GetValue(optionName);

    if (!string.IsNullOrWhiteSpace(value))
    {
        return value;
    }

    value = Environment.GetEnvironmentVariable(environmentName);

    if (!string.IsNullOrWhiteSpace(value))
    {
        return value;
    }

    Console.Write($"{prompt}: ");
    value = ReadSecret();

    if (string.IsNullOrWhiteSpace(value))
    {
        throw new InvalidOperationException($"{environmentName} is required.");
    }

    return value;
}

static string GetValue(
    CommandLineOptions options,
    string optionName,
    string environmentName,
    string defaultValue)
{
    var value = options.GetValue(optionName);

    if (!string.IsNullOrWhiteSpace(value))
    {
        return value;
    }

    value = Environment.GetEnvironmentVariable(environmentName);

    return string.IsNullOrWhiteSpace(value) ? defaultValue : value;
}

static string? GetNullableValue(
    CommandLineOptions options,
    string optionName,
    string environmentName,
    string? defaultValue)
{
    var value = options.GetValue(optionName);

    if (value is not null)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value;
    }

    value = Environment.GetEnvironmentVariable(environmentName);

    if (value is not null)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value;
    }

    return defaultValue;
}

static decimal GetDecimal(
    CommandLineOptions options,
    string optionName,
    string environmentName,
    decimal defaultValue)
{
    var value = options.GetValue(optionName)
        ?? Environment.GetEnvironmentVariable(environmentName);

    if (string.IsNullOrWhiteSpace(value))
    {
        return defaultValue;
    }

    if (decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out var result))
    {
        return result;
    }

    throw new InvalidOperationException($"{optionName} must be a valid decimal number.");
}

static string ReadSecret()
{
    var secret = new StringBuilder();

    try
    {
        while (true)
        {
            var key = Console.ReadKey(intercept: true);

            if (key.Key == ConsoleKey.Enter)
            {
                Console.WriteLine();
                return secret.ToString();
            }

            if (key.Key == ConsoleKey.Backspace)
            {
                if (secret.Length > 0)
                {
                    secret.Length--;
                    Console.Write("\b \b");
                }

                continue;
            }

            if (!char.IsControl(key.KeyChar))
            {
                secret.Append(key.KeyChar);
                Console.Write("*");
            }
        }
    }
    catch (InvalidOperationException)
    {
        return Console.ReadLine()?.Trim() ?? string.Empty;
    }
}

static void PrintCreateResponse(CreatePaymentResponse response)
{
    Console.WriteLine($"Payment ID: {response.PaymentId}");
    Console.WriteLine($"Amount: {response.Amount.ToString(CultureInfo.InvariantCulture)}");
    Console.WriteLine($"Payment link: {response.PaymentLink}");
    Console.WriteLine($"Expires at: {response.ExpiresAt.ToString("O", CultureInfo.InvariantCulture)}");
}

static void PrintVerifyResponse(VerifyPaymentResponse response)
{
    Console.WriteLine($"Payment ID: {response.PaymentId}");
    Console.WriteLine($"Status: {response.Status}");
    Console.WriteLine($"Amount: {response.Amount.ToString(CultureInfo.InvariantCulture)}");
    Console.WriteLine($"Paid amount: {response.PaidAmount.ToString(CultureInfo.InvariantCulture)}");
    Console.WriteLine($"Fee amount: {response.FeeAmount.ToString(CultureInfo.InvariantCulture)}");
    Console.WriteLine($"Expected amount: {response.ExpectedAmount.ToString(CultureInfo.InvariantCulture)}");
    Console.WriteLine($"Merchant receivable amount: {response.MerchantReceivableAmount.ToString(CultureInfo.InvariantCulture)}");
    Console.WriteLine($"Currency: {response.Currency ?? "(empty)"}");
    Console.WriteLine($"Network: {response.Network ?? "(empty)"}");
    Console.WriteLine($"Expires at: {response.ExpiresAt.ToString("O", CultureInfo.InvariantCulture)}");
}

static void PrintUsage()
{
    Console.WriteLine("PayVelix sandbox console");
    Console.WriteLine();
    Console.WriteLine("Usage:");
    Console.WriteLine("  dotnet run --project .\\PayVelix.SandboxConsole -- [options]");
    Console.WriteLine();
    Console.WriteLine("Options:");
    Console.WriteLine("  --api-key <value>       Sandbox API key. Prefer PAYVELIX_API_KEY instead.");
    Console.WriteLine("  --base-url <value>      API base URL. Default: https://api.payvelix.com");
    Console.WriteLine("  --amount <value>        Payment amount. Default: 1");
    Console.WriteLine("  --currency <value>      Payment currency. Default: USD");
    Console.WriteLine("  --return-url <value>    Return URL. Default: https://example.com/payvelix/return");
    Console.WriteLine("  --webhook-url <value>   Optional webhook URL.");
    Console.WriteLine("  --skip-verify           Do not ask to verify the created payment.");
    Console.WriteLine("  --help                  Show this help.");
    Console.WriteLine();
    Console.WriteLine("Environment variables:");
    Console.WriteLine("  PAYVELIX_API_KEY, PAYVELIX_BASE_URL, PAYVELIX_AMOUNT, PAYVELIX_CURRENCY");
    Console.WriteLine("  PAYVELIX_RETURN_URL, PAYVELIX_WEBHOOK_URL");
}

internal sealed class CommandLineOptions
{
    private readonly Dictionary<string, string?> _values;

    private CommandLineOptions(Dictionary<string, string?> values)
    {
        _values = values;
    }

    public static CommandLineOptions Parse(string[] args)
    {
        var values = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);

        for (var index = 0; index < args.Length; index++)
        {
            var argument = args[index];

            if (!argument.StartsWith("--", StringComparison.Ordinal))
            {
                continue;
            }

            var name = argument[2..];

            if (index + 1 < args.Length && !args[index + 1].StartsWith("--", StringComparison.Ordinal))
            {
                values[name] = args[index + 1];
                index++;
            }
            else
            {
                values[name] = null;
            }
        }

        return new CommandLineOptions(values);
    }

    public string? GetValue(string name)
    {
        return _values.TryGetValue(name, out var value) ? value : null;
    }

    public bool HasFlag(string name)
    {
        return _values.ContainsKey(name);
    }
}
