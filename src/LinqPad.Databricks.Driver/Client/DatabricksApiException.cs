using System.Net;

namespace LinqPad.Databricks.Driver.Client;

public class DatabricksApiException : Exception
{
    public string ErrorCode { get; }
    public HttpStatusCode StatusCode { get; }

    public DatabricksApiException(string errorCode, string message, HttpStatusCode statusCode)
        : base($"[{errorCode}] {message}")
    {
        ErrorCode = errorCode;
        StatusCode = statusCode;
    }
}
