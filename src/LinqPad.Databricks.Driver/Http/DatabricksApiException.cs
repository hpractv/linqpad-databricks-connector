using System;

namespace LinqPad.Databricks.Driver.Http
{
    public class DatabricksApiException : Exception
    {
        public string? ErrorCode { get; }
        public int StatusCode { get; }

        public DatabricksApiException(string message, string? errorCode, int statusCode)
            : base(message)
        {
            ErrorCode = errorCode;
            StatusCode = statusCode;
        }
    }
}
