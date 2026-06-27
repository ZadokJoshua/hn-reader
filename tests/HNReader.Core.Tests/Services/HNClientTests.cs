using HNReader.Core.Enums;
using HNReader.Core.Services;

namespace HNReader.Core.Tests.Services;

public class HNClientTests
{
    [Fact]
    public void ClearCache_IsCallable()
    {
        // Arrange - Create a real HttpClient with a test handler
        var handler = new MockHttpMessageHandler();
        var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://hacker-news.firebaseio.com/v0/")
        };
        var client = new HNClient(httpClient);

        // Act & Assert - Should not throw
        client.ClearCache();
    }

    /// <summary>
    /// Mock HTTP handler for testing without real network calls.
    /// </summary>
    private class MockHttpMessageHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, 
            CancellationToken cancellationToken)
        {
            var response = new HttpResponseMessage(System.Net.HttpStatusCode.OK);
            response.Content = new StringContent("[]");
            return Task.FromResult(response);
        }
    }
}

