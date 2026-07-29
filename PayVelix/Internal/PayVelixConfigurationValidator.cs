namespace PayVelix.Internal;

internal static class PayVelixConfigurationValidator
{
    public static string ValidateApiKey(string? apiKey)
    {
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            throw new ArgumentException("PayVelix API key is required.", nameof(apiKey));
        }

        return apiKey;
    }

    public static Uri ValidateBaseUrl(string? baseUrl)
    {
        if (string.IsNullOrWhiteSpace(baseUrl))
        {
            throw new ArgumentException("PayVelix BaseUrl is required.", nameof(baseUrl));
        }

        if (!Uri.TryCreate(baseUrl, UriKind.Absolute, out var uri)
            || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
        {
            throw new ArgumentException("PayVelix BaseUrl must be an absolute HTTP or HTTPS URL.", nameof(baseUrl));
        }

        return new Uri($"{uri.AbsoluteUri.TrimEnd('/')}/");
    }

    public static TimeSpan ValidateTimeout(TimeSpan timeout)
    {
        if (timeout <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(timeout), timeout, "PayVelix Timeout must be greater than zero.");
        }

        return timeout;
    }
}
