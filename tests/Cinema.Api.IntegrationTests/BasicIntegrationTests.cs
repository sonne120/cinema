using System.Net;
using Cinema.Api.IntegrationTests.Infrastructure;
using FluentAssertions;
using Xunit;

namespace Cinema.Api.IntegrationTests;

[Collection("IntegrationTests")]
public class HealthCheckTests
{
    private readonly HttpClient _client;

    public HealthCheckTests(CinemaWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task HealthCheck_ShouldReturnHealthy()
    {
        var response = await _client.GetAsync("/health");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }
}

[Collection("IntegrationTests")]
public class ApiEndpointTests
{
    private readonly HttpClient _client;

    public ApiEndpointTests(CinemaWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task ShowtimesEndpoint_ShouldBeAccessible()
    {
        var response = await _client.GetAsync("/api/showtimes");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }
}
