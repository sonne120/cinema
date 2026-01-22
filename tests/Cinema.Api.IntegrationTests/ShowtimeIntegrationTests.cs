using System.Net;
using System.Net.Http.Json;
using Cinema.Api.IntegrationTests.Infrastructure;
using FluentAssertions;
using Xunit;

namespace Cinema.Api.IntegrationTests;

[Collection("IntegrationTests")]
public class ShowtimeIntegrationTests
{
    private readonly HttpClient _client;

    public ShowtimeIntegrationTests(CinemaWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task CreateShowtime_WithValidData_ShouldReturnCreated()
    {
        var request = new
        {
            movieImdbId = "tt0111161",
            screeningTime = DateTime.UtcNow.AddDays(1),
            auditoriumId = Guid.NewGuid()
        };

        var response = await _client.PostAsJsonAsync("/api/showtimes", request);

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var content = await response.Content.ReadFromJsonAsync<ShowtimeResponseDto>();
        content.Should().NotBeNull();
        content!.Id.Should().NotBeEmpty();
    }

    [Fact]
    public async Task GetShowtime_WithValidId_ShouldReturnShowtime()
    {
        // First create a showtime
        var createRequest = new
        {
            movieImdbId = "tt0468569",
            screeningTime = DateTime.UtcNow.AddDays(2),
            auditoriumId = Guid.NewGuid()
        };
        var createResponse = await _client.PostAsJsonAsync("/api/showtimes", createRequest);
        createResponse.EnsureSuccessStatusCode();
        var created = await createResponse.Content.ReadFromJsonAsync<ShowtimeResponseDto>();

        var response = await _client.GetAsync($"/api/showtimes/{created!.Id}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var showtime = await response.Content.ReadFromJsonAsync<ShowtimeResponseDto>();
        showtime.Should().NotBeNull();
        showtime!.Id.Should().Be(created.Id);
    }

    [Fact]
    public async Task GetShowtime_WithInvalidId_ShouldReturnNotFound()
    {
        var response = await _client.GetAsync($"/api/showtimes/{Guid.NewGuid()}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task ListShowtimes_ShouldReturnOk()
    {
        var response = await _client.GetAsync("/api/showtimes");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    private record ShowtimeResponseDto(
        Guid Id,
        MovieDetailsDto MovieDetails,
        DateTime ScreeningTime,
        Guid AuditoriumId,
        string Status,
        DateTime CreatedAt);

    private record MovieDetailsDto(
        string ImdbId,
        string Title,
        string? PosterUrl,
        int? ReleaseYear);
}
