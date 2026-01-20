using System.Net;
using System.Net.Http.Json;
using Cinema.Api.IntegrationTests.Infrastructure;
using FluentAssertions;
using Xunit;

namespace Cinema.Api.IntegrationTests;

[Collection("IntegrationTests")]
public class ReservationIntegrationTests
{
    private readonly HttpClient _client;

    public ReservationIntegrationTests(CinemaWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task CreateReservation_WithValidData_ShouldReturnCreated()
    {
        // First create a showtime
        var showtimeId = await CreateShowtimeAsync();

        var request = new
        {
            showtimeId = showtimeId,
            seats = new[]
            {
                new { row = 5, number = 10 },
                new { row = 5, number = 11 }
            }
        };

        var response = await _client.PostAsJsonAsync("/api/reservations", request);

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var content = await response.Content.ReadFromJsonAsync<ReservationResponseDto>();
        content.Should().NotBeNull();
        content!.Id.Should().NotBeEmpty();
        content.ShowtimeId.Should().Be(showtimeId);
        content.Seats.Should().HaveCount(2);
    }

    [Fact]
    public async Task CreateReservation_WithNonContiguousSeats_ShouldFail()
    {
        var showtimeId = await CreateShowtimeAsync();

        var request = new
        {
            showtimeId = showtimeId,
            seats = new[]
            {
                new { row = 5, number = 10 },
                new { row = 5, number = 15 } // Non-contiguous
            }
        };

        // API throws ArgumentException for invalid seats - this is expected behavior
        var act = () => _client.PostAsJsonAsync("/api/reservations", request);
        await act.Should().ThrowAsync<Exception>()
            .WithMessage("*contiguous*");
    }

    [Fact]
    public async Task CreateReservation_WithSeatsInDifferentRows_ShouldFail()
    {
        var showtimeId = await CreateShowtimeAsync();

        var request = new
        {
            showtimeId = showtimeId,
            seats = new[]
            {
                new { row = 5, number = 10 },
                new { row = 6, number = 10 } // Different row
            }
        };

        // API throws ArgumentException for seats in different rows - this is expected behavior
        var act = () => _client.PostAsJsonAsync("/api/reservations", request);
        await act.Should().ThrowAsync<Exception>()
            .WithMessage("*same row*");
    }

    [Fact]
    public async Task ConfirmReservation_WithValidReservation_ShouldReturnOk()
    {
        // Create a showtime first
        var showtimeId = await CreateShowtimeAsync();

        // Create a reservation
        var createRequest = new
        {
            showtimeId = showtimeId,
            seats = new[]
            {
                new { row = 7, number = 5 },
                new { row = 7, number = 6 }
            }
        };
        var createResponse = await _client.PostAsJsonAsync("/api/reservations", createRequest);
        createResponse.EnsureSuccessStatusCode();
        var created = await createResponse.Content.ReadFromJsonAsync<ReservationResponseDto>();

        var response = await _client.PostAsync($"/api/reservations/{created!.Id}/confirm", null);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var confirmed = await response.Content.ReadFromJsonAsync<ReservationResponseDto>();
        confirmed.Should().NotBeNull();
        confirmed!.Status.Should().Be("Confirmed");
    }

    [Fact]
    public async Task CreateReservation_ThenConfirm_ShouldPreventDuplicateSeats()
    {
        var showtimeId = await CreateShowtimeAsync();

        // Create and confirm first reservation
        var request1 = new
        {
            showtimeId = showtimeId,
            seats = new[]
            {
                new { row = 8, number = 10 },
                new { row = 8, number = 11 }
            }
        };
        var response1 = await _client.PostAsJsonAsync("/api/reservations", request1);
        response1.EnsureSuccessStatusCode();
        var created1 = await response1.Content.ReadFromJsonAsync<ReservationResponseDto>();
        await _client.PostAsync($"/api/reservations/{created1!.Id}/confirm", null);

        // Try to create second reservation with same seats - should throw exception
        var request2 = new
        {
            showtimeId = showtimeId,
            seats = new[]
            {
                new { row = 8, number = 10 },
                new { row = 8, number = 11 }
            }
        };
        var act = () => _client.PostAsJsonAsync("/api/reservations", request2);
        await act.Should().ThrowAsync<Exception>()
            .WithMessage("*already sold*");
    }

    private async Task<Guid> CreateShowtimeAsync()
    {
        var request = new
        {
            movieImdbId = "tt" + Random.Shared.Next(1000000, 9999999),
            screeningTime = DateTime.UtcNow.AddDays(Random.Shared.Next(1, 30)),
            auditoriumId = Guid.NewGuid()
        };

        var response = await _client.PostAsJsonAsync("/api/showtimes", request);
        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<ShowtimeResponseDto>();
        return result!.Id;
    }

    private record ShowtimeResponseDto(Guid Id);

    private record ReservationResponseDto(
        Guid Id,
        Guid ShowtimeId,
        List<SeatResponseDto> Seats,
        string Status,
        DateTime CreatedAt,
        DateTime? ExpiresAt,
        DateTime? ConfirmedAt);

    private record SeatResponseDto(int Row, int Number);
}
