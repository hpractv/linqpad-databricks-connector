using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace LinqPad.Databricks.Driver.Client;

public class DatabricksHttpClient : IDisposable
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    // Azure pattern: adb-<workspace-id>.<region>.azuredatabricks.net (dots allowed in subdomain)
    // Also accept generic *.azuredatabricks.net and *.databricks.azure.cn (China)
    private static readonly Regex WorkspaceUrlPattern = new(
        @"^https://[a-zA-Z0-9\-\.]+(\.azuredatabricks\.net|\.databricks\.azure\.cn)/?$",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private readonly HttpClient _http;

    public DatabricksHttpClient(string workspaceUrl, string personalAccessToken)
        : this(workspaceUrl, personalAccessToken, null)
    {
    }

    // Internal constructor allows injecting a custom handler for testing.
    internal DatabricksHttpClient(string workspaceUrl, string personalAccessToken, HttpMessageHandler? handler)
    {
        if (string.IsNullOrWhiteSpace(workspaceUrl))
            throw new ArgumentException("Workspace URL must not be empty.", nameof(workspaceUrl));
        if (string.IsNullOrWhiteSpace(personalAccessToken))
            throw new ArgumentException("Personal Access Token must not be empty.", nameof(personalAccessToken));

        string normalizedUrl = workspaceUrl.TrimEnd('/');

        if (!WorkspaceUrlPattern.IsMatch(normalizedUrl) && !WorkspaceUrlPattern.IsMatch(normalizedUrl + "/"))
            throw new ArgumentException(
                $"Workspace URL '{workspaceUrl}' is not a valid Azure Databricks workspace URL. " +
                "Expected format: https://adb-<id>.<region>.azuredatabricks.net",
                nameof(workspaceUrl));

        _http = handler != null ? new HttpClient(handler) : new HttpClient();
        _http.BaseAddress = new Uri(normalizedUrl.EndsWith('/') ? normalizedUrl : normalizedUrl + "/");
        _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", personalAccessToken);
        _http.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
    }

    public async Task<T> GetAsync<T>(string relativeUrl, CancellationToken ct = default)
    {
        HttpResponseMessage response = await _http.GetAsync(relativeUrl, ct).ConfigureAwait(false);
        return await ReadResponseAsync<T>(response, ct).ConfigureAwait(false);
    }

    public async Task<T> PostAsync<T>(string relativeUrl, object body, CancellationToken ct = default)
    {
        string json = JsonSerializer.Serialize(body, JsonOptions);
        using var content = new StringContent(json, Encoding.UTF8, "application/json");
        HttpResponseMessage response = await _http.PostAsync(relativeUrl, content, ct).ConfigureAwait(false);
        return await ReadResponseAsync<T>(response, ct).ConfigureAwait(false);
    }

    private static async Task<T> ReadResponseAsync<T>(HttpResponseMessage response, CancellationToken ct)
    {
        string body = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);

        if (!response.IsSuccessStatusCode)
        {
            // Try to parse Databricks error envelope { "error_code": "...", "message": "..." }
            string errorCode = "UNKNOWN_ERROR";
            string message = body;
            try
            {
                using JsonDocument doc = JsonDocument.Parse(body);
                if (doc.RootElement.TryGetProperty("error_code", out JsonElement ec))
                    errorCode = ec.GetString() ?? errorCode;
                if (doc.RootElement.TryGetProperty("message", out JsonElement msg))
                    message = msg.GetString() ?? message;
            }
            catch (JsonException) { /* use raw body */ }

            throw new DatabricksApiException(errorCode, message, response.StatusCode);
        }

        try
        {
            return JsonSerializer.Deserialize<T>(body, JsonOptions)
                ?? throw new DatabricksApiException("EMPTY_RESPONSE", "Server returned an empty response.", HttpStatusCode.OK);
        }
        catch (JsonException ex)
        {
            throw new DatabricksApiException("DESERIALIZATION_ERROR", ex.Message, response.StatusCode);
        }
    }

    public void Dispose() => _http.Dispose();
}
