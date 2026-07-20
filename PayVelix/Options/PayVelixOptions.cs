namespace PayVelix.Options;

public sealed class PayVelixOptions
{
    public string ApiKey { get; set; } = string.Empty;

    public string BaseUrl { get; set; } = "https://api.payvelix.com";

    public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(30);
}
