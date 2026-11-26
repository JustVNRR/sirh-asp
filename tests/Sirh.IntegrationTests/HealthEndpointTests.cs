using System.Net;
using System.Text.Json;
using Shouldly;
using Xunit;

namespace Sirh.IntegrationTests;

public sealed class HealthEndpointTests : IClassFixture<ApiWebApplicationFactory>
{
    private readonly HttpClient _client;

    public HealthEndpointTests(ApiWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Get_Health_Should_Return_Healthy_Json_And_Correlation_Header()
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "/health");
        request.Headers.Add("X-Correlation-Id", "it-health-check-001");

        var response = await _client.SendAsync(request);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        response.Headers.GetValues("X-Correlation-Id").ShouldContain("it-health-check-001");

        var content = await response.Content.ReadAsStringAsync();
        using var json = JsonDocument.Parse(content);
        json.RootElement.GetProperty("status").GetString().ShouldBe("Healthy");
        json.RootElement.GetProperty("checks").GetArrayLength().ShouldBeGreaterThanOrEqualTo(2);
    }
}
