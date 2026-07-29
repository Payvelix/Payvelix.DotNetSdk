using System.Net;
using System.Text.Json;
using System.Text.Json.Serialization;
using PayVelix.Contracts.Common;

namespace PayVelix.Internal;

internal static class PayVelixHttp
{
    public static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() }
    };

    public static T DeserializeSuccessResponse<T>(
        string responseBody,
        HttpStatusCode statusCode,
        string operationName)
    {
        if (string.IsNullOrWhiteSpace(responseBody))
        {
            throw new PayVelixApiException(
                "PayVelix returned an empty response.",
                statusCode,
                responseBody: responseBody);
        }

        var result = JsonSerializer.Deserialize<T>(
            responseBody,
            JsonOptions);

        return result ?? throw new PayVelixApiException(
            $"Unable to deserialize PayVelix {operationName} response.",
            statusCode,
            responseBody: responseBody);
    }

    public static PayVelixApiException CreateApiException(
        HttpStatusCode statusCode,
        string responseBody)
    {
        var error = TryDeserializeError(responseBody);

        return new PayVelixApiException(
            error?.Message ?? "PayVelix API request failed.",
            statusCode,
            error?.Code,
            responseBody);
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
