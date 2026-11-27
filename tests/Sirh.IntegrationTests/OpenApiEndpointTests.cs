using System.Net;
using System.Text.Json;
using Shouldly;
using Xunit;

namespace Sirh.IntegrationTests;

public sealed class OpenApiEndpointTests : IClassFixture<ApiWebApplicationFactory>
{
    private readonly HttpClient _client;

    public OpenApiEndpointTests(ApiWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task OpenApi_Document_Should_Expose_Bearer_Security_Scheme_And_Global_Requirement()
    {
        var response = await _client.GetAsync("/openapi/v1.json");
        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        var content = await response.Content.ReadAsStringAsync();
        using var json = JsonDocument.Parse(content);

        var bearer = json.RootElement
            .GetProperty("components")
            .GetProperty("securitySchemes")
            .GetProperty("Bearer");

        bearer.GetProperty("type").GetString().ShouldBe("http");
        bearer.GetProperty("scheme").GetString().ShouldBe("bearer");
        bearer.GetProperty("bearerFormat").GetString().ShouldBe("JWT");

        var securityArray = json.RootElement.GetProperty("security");
        securityArray.GetArrayLength().ShouldBeGreaterThan(0);
        securityArray.EnumerateArray()
            .Any(item => item.TryGetProperty("Bearer", out _))
            .ShouldBeTrue();
    }
}
