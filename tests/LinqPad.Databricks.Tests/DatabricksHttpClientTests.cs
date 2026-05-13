using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using LinqPad.Databricks.Driver.Client;
using Moq;
using Moq.Protected;

namespace LinqPad.Databricks.Tests;

public class DatabricksHttpClientTests
{
    // ─── URL validation ───────────────────────────────────────────────────────

    [Theory]
    [InlineData("https://adb-1234567890.1.azuredatabricks.net")]
    [InlineData("https://adb-1234567890.1.azuredatabricks.net/")]
    [InlineData("https://myworkspace.azuredatabricks.net")]
    public void Constructor_ValidUrl_DoesNotThrow(string url)
    {
        var ex = Record.Exception(() => new DatabricksHttpClient(url, "token", CreateHandler(HttpStatusCode.OK, "{}")));
        Assert.Null(ex);
    }

    [Theory]
    [InlineData("http://adb-1234567890.1.azuredatabricks.net")]   // not https
    [InlineData("https://not-databricks.example.com")]
    [InlineData("ftp://adb-1234.azuredatabricks.net")]
    [InlineData("")]
    [InlineData("   ")]
    public void Constructor_InvalidUrl_ThrowsArgumentException(string url)
    {
        Assert.Throws<ArgumentException>(() =>
            new DatabricksHttpClient(url, "token", CreateHandler(HttpStatusCode.OK, "{}")));
    }

    [Fact]
    public void Constructor_EmptyPat_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() =>
            new DatabricksHttpClient("https://adb-1.azuredatabricks.net", "", CreateHandler(HttpStatusCode.OK, "{}")));
    }

    // ─── Successful GET ───────────────────────────────────────────────────────

    [Fact]
    public async Task GetAsync_SuccessResponse_DeserializesJson()
    {
        var expected = new { name = "my-catalog", comment = "test" };
        string json = JsonSerializer.Serialize(expected);
        using var client = new DatabricksHttpClient(
            "https://adb-1.azuredatabricks.net", "token", CreateHandler(HttpStatusCode.OK, json));

        var result = await client.GetAsync<Dictionary<string, string>>("api/test");

        Assert.Equal("my-catalog", result["name"]);
        Assert.Equal("test", result["comment"]);
    }

    // ─── Error envelope ───────────────────────────────────────────────────────

    [Fact]
    public async Task GetAsync_ErrorResponse_ThrowsDatabricksApiException()
    {
        string errorJson = """{"error_code":"PERMISSION_DENIED","message":"Access denied"}""";
        using var client = new DatabricksHttpClient(
            "https://adb-1.azuredatabricks.net", "token", CreateHandler(HttpStatusCode.Forbidden, errorJson));

        var ex = await Assert.ThrowsAsync<DatabricksApiException>(
            () => client.GetAsync<Dictionary<string, string>>("api/test"));

        Assert.Equal("PERMISSION_DENIED", ex.ErrorCode);
        Assert.Contains("Access denied", ex.Message);
        Assert.Equal(HttpStatusCode.Forbidden, ex.StatusCode);
    }

    [Fact]
    public async Task GetAsync_NonJsonErrorResponse_ThrowsDatabricksApiException()
    {
        using var client = new DatabricksHttpClient(
            "https://adb-1.azuredatabricks.net", "token", CreateHandler(HttpStatusCode.InternalServerError, "Internal Server Error"));

        var ex = await Assert.ThrowsAsync<DatabricksApiException>(
            () => client.GetAsync<Dictionary<string, string>>("api/test"));

        Assert.Equal("UNKNOWN_ERROR", ex.ErrorCode);
    }

    // ─── Successful POST ──────────────────────────────────────────────────────

    [Fact]
    public async Task PostAsync_SuccessResponse_DeserializesJson()
    {
        string json = """{"statement_id":"abc-123","status":{"state":"SUCCEEDED"}}""";
        using var client = new DatabricksHttpClient(
            "https://adb-1.azuredatabricks.net", "token", CreateHandler(HttpStatusCode.OK, json));

        var result = await client.PostAsync<Dictionary<string, object>>("api/test", new { foo = "bar" });

        Assert.True(result.ContainsKey("statement_id"));
    }

    // ─── Helpers ──────────────────────────────────────────────────────────────

    internal static HttpMessageHandler CreateHandler(HttpStatusCode statusCode, string body)
    {
        var mock = new Mock<HttpMessageHandler>();
        mock.Protected()
            .Setup<Task<HttpResponseMessage>>("SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage(statusCode)
            {
                Content = new StringContent(body, Encoding.UTF8, "application/json")
            });
        return mock.Object;
    }
}
