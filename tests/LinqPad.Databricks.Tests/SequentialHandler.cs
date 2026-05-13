using System.Net;
using System.Net.Http;
using System.Text;

namespace LinqPad.Databricks.Tests;

/// <summary>
/// A test HTTP handler that returns pre-configured responses in sequence.
/// </summary>
internal sealed class SequentialHandler : HttpMessageHandler
{
    private readonly (HttpStatusCode Status, string Body)[] _responses;
    private int _index;

    public SequentialHandler(IEnumerable<(HttpStatusCode, string)> responses)
    {
        _responses = responses.ToArray();
    }

    public int CallCount => _index;

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        if (_index >= _responses.Length)
            throw new InvalidOperationException($"SequentialHandler ran out of responses after {_responses.Length} calls. Request: {request.RequestUri}");

        var (status, body) = _responses[_index++];
        return Task.FromResult(new HttpResponseMessage(status)
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json")
        });
    }
}
