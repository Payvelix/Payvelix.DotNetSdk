using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Serialization;
using PayVelix.Contracts.Common;

namespace PayVelix.Internal;

internal static class PayVelixHttp
{
    public const string HttpClientName = "PayVelix";

    public static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() }
    };

    public static void ConfigureSharedHeaders(HttpClient httpClient)
    {
        httpClient.DefaultRequestHeaders.TryAddWithoutValidation(
            "User-Agent",
            $"PayVelix.DotNetSdk/{PayVelixSdkVersion.Current}");
        httpClient.DefaultRequestHeaders.TryAddWithoutValidation(
            "X-SDK-Version",
            PayVelixSdkVersion.Current);
        httpClient.DefaultRequestHeaders.Accept.Add(
            new MediaTypeWithQualityHeaderValue("application/json"));
    }

    public static void AddApiKey(HttpRequestMessage request, string apiKey)
    {
        request.Headers.TryAddWithoutValidation("X-Api-Key", apiKey);
    }

    public static T DeserializeSuccessResponse<T>(
        string responseBody,
        System.Net.HttpStatusCode statusCode,
        string operationName)
    {
        if (string.IsNullOrWhiteSpace(responseBody))
        {
            throw new PayVelixApiException(
                "PayVelix returned an empty response.",
                statusCode,
                responseBody: responseBody);
        }

        try
        {
            var result = JsonSerializer.Deserialize<T>(
                responseBody,
                JsonOptions);

            return result ?? throw new PayVelixApiException(
                $"Unable to deserialize PayVelix {operationName} response.",
                statusCode,
                responseBody: responseBody);
        }
        catch (JsonException exception)
        {
            throw new PayVelixApiException(
                $"PayVelix returned malformed JSON for {operationName}.",
                statusCode,
                responseBody: responseBody,
                innerException: exception);
        }
    }

    public static PayVelixApiException CreateApiException(
        System.Net.HttpStatusCode statusCode,
        string responseBody,
        string apiKey)
    {
        var error = TryDeserializeError(responseBody);

        return new PayVelixApiException(
            RedactApiKey(error?.Message ?? "PayVelix API request failed.", apiKey),
            statusCode,
            error?.Code,
            RedactApiKey(responseBody, apiKey));
    }

    private static string RedactApiKey(string value, string apiKey)
    {
        return string.IsNullOrEmpty(apiKey)
            ? value
            : value.Replace(apiKey, "[REDACTED]", StringComparison.Ordinal);
    }

    private static PayVelixErrorResponse? TryDeserializeError(string responseBody)
    {
        try
        {
            var errorEnvelope = JsonSerializer.Deserialize<PayVelixErrorEnvelope>(
                responseBody,
                JsonOptions);

            if (errorEnvelope?.Error is not null)
            {
                return errorEnvelope.Error;
            }
        }
        catch
        {
            // Preserve raw response body even if it is not valid JSON.
        }

        try
        {
            return JsonSerializer.Deserialize<PayVelixErrorResponse>(
                responseBody,
                JsonOptions);
        }
        catch
        {
            // Preserve raw response body even if it is not valid JSON.
            return null;
        }
    }
}
