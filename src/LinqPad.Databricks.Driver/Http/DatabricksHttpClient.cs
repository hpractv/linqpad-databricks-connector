using System;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Text.Json.Serialization;
using System.Net.Http.Headers;
using System.Net;

namespace LinqPad.Databricks.Driver.Http
{
    public sealed class DatabricksHttpClient : IDisposable
    {
        private readonly HttpClient _httpClient;
        private readonly JsonSerializerOptions _jsonOptions;
        private readonly string _workspaceBaseUrl;

        private static readonly System.Text.RegularExpressions.Regex UrlRegex = new(
            @"^https://[a-zA-Z0-9\-\.]+(\.(azuredatabricks\.net|databricks\.azure\.cn))/?$",
            System.Text.RegularExpressions.RegexOptions.Compiled | System.Text.RegularExpressions.RegexOptions.IgnoreCase);

        public DatabricksHttpClient(string workspaceUrl, string pat)
            : this(workspaceUrl, pat, handler: null)
        {
        }

        // Internal constructor for tests to inject a handler
        internal DatabricksHttpClient(string workspaceUrl, string pat, HttpMessageHandler? handler)
        {
            if (string.IsNullOrWhiteSpace(workspaceUrl))
                throw new ArgumentException("workspaceUrl is required", nameof(workspaceUrl));
            if (string.IsNullOrWhiteSpace(pat))
                throw new ArgumentException("pat is required", nameof(pat));

            if (!UrlRegex.IsMatch(workspaceUrl))
                throw new ArgumentException("workspaceUrl does not match expected Databricks workspace URL pattern", nameof(workspaceUrl));

            _workspaceBaseUrl = workspaceUrl.TrimEnd('/');

            _httpClient = handler is null ? new HttpClient() : new HttpClient(handler, disposeHandler: false);
            _httpClient.BaseAddress = new Uri(_workspaceBaseUrl);
            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", pat);

            _jsonOptions = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            };
        }

        public async Task<T?> GetAsync<T>(string path, CancellationToken ct = default)
        {
            using var resp = await _httpClient.GetAsync(path, ct).ConfigureAwait(false);
            var content = await resp.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
            if (resp.IsSuccessStatusCode)
            {
                if (string.IsNullOrWhiteSpace(content))
                    return default;
                return JsonSerializer.Deserialize<T>(content, _jsonOptions);
            }

            await ThrowForNonSuccess(resp.StatusCode, content).ConfigureAwait(false);
            return default; // unreachable
        }

        public async Task<T?> PostAsync<T>(string path, object body, CancellationToken ct = default)
        {
            var json = JsonSerializer.Serialize(body);
            using var content = new StringContent(json, Encoding.UTF8, "application/json");
            using var resp = await _httpClient.PostAsync(path, content, ct).ConfigureAwait(false);
            var respBody = await resp.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
            if (resp.IsSuccessStatusCode)
            {
                if (string.IsNullOrWhiteSpace(respBody))
                    return default;
                return JsonSerializer.Deserialize<T>(respBody, _jsonOptions);
            }

            await ThrowForNonSuccess(resp.StatusCode, respBody).ConfigureAwait(false);
            return default; // unreachable
        }

        private static Task ThrowForNonSuccess(HttpStatusCode statusCode, string responseBody)
        {
            // Try parse JSON for known fields: message, error, error_code
            string message = $"Databricks API returned {(int)statusCode}";
            string? errorCode = null;

            if (!string.IsNullOrWhiteSpace(responseBody))
            {
                try
                {
                    using var doc = JsonDocument.Parse(responseBody);
                    if (doc.RootElement.ValueKind == JsonValueKind.Object)
                    {
                        if (doc.RootElement.TryGetProperty("message", out var m) && m.ValueKind == JsonValueKind.String)
                        {
                            message = m.GetString() ?? message;
                        }
                        else if (doc.RootElement.TryGetProperty("error", out var e) && e.ValueKind == JsonValueKind.String)
                        {
                            message = e.GetString() ?? message;
                        }

                        if (doc.RootElement.TryGetProperty("error_code", out var ec) && ec.ValueKind == JsonValueKind.String)
                        {
                            errorCode = ec.GetString();
                        }
                    }
                }
                catch { /* ignore parse errors */ }
            }

            throw new DatabricksApiException(message, errorCode, (int)statusCode);
        }

        public void Dispose()
        {
            _httpClient?.Dispose();
        }
    }
}
