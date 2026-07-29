using System.Net;

namespace PayVelix.Contracts.Common;

public sealed class PayVelixApiException : Exception
{
    public HttpStatusCode StatusCode { get; }

    public string? ErrorCode { get; }

    public string? ResponseBody { get; }

    public PayVelixApiException(
        string message,
        HttpStatusCode statusCode,
        string? errorCode = null,
        string? responseBody = null)
        : base(message)
    {
        StatusCode = statusCode;
        ErrorCode = errorCode;
        ResponseBody = responseBody;
    }
}
