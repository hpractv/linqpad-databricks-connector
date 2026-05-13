using System;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using LinqPad.Databricks.Driver.Http;

namespace LinqPad.Databricks.Tests
{
    public class DatabricksHttpClientTests
    {
        private class FakeHandler : DelegatingHandler
        {
            private readonly HttpResponseMessage _response;
            public FakeHandler(HttpResponseMessage response)
            {
                _response = response;
            }

            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                return Task.FromResult(_response);
            }
        }

        private class TestModel { public string? Foo { get; set; } }

        [Fact]
        public async Task GetAsync_ReturnsDeserialized()
        {
            var json = "{ \"foo\": \"bar\" }";
            var resp = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(json)
            };

            using var client = new DatabricksHttpClient("https://adb-123.azuredatabricks.net", "pat", new FakeHandler(resp));
            var result = await client.GetAsync<TestModel>("/api/test");
            Assert.NotNull(result);
            Assert.Equal("bar", result!.Foo);
        }

        [Fact]
        public async Task GetAsync_ErrorResponse_ThrowsDatabricksApiException()
        {
            var json = "{ \"error\": \"Bad things happened\", \"error_code\": \"BAD_1\" }";
            var resp = new HttpResponseMessage(HttpStatusCode.BadRequest)
            {
                Content = new StringContent(json)
            };

            using var client = new DatabricksHttpClient("https://adb-123.azuredatabricks.net", "pat", new FakeHandler(resp));
            var ex = await Assert.ThrowsAsync<DatabricksApiException>(async () => await client.GetAsync<TestModel>("/api/test"));
            Assert.Contains("Bad things", ex.Message);
            Assert.Equal("BAD_1", ex.ErrorCode);
            Assert.Equal(400, ex.StatusCode);
        }
    }
}
