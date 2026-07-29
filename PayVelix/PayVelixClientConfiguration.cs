namespace PayVelix;

public sealed record PayVelixClientConfiguration
{
    public required string ApiKey { get; init; }

    public string? BaseUrl { get; init; }

    public TimeSpan? Timeout { get; init; }
}
