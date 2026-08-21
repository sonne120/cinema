using Cinema.Contracts.Showtimes;
using Cinema.Domain.ShowtimeAggregate;

namespace Cinema.Api.Mapping;

public static class ShowtimeMappings
{
    public static ShowtimeResponse ToResponse(this Showtime showtime) =>
        new(showtime.Id.Value,
            new MovieDetailsResponse(
                showtime.MovieDetails.ImdbId,
                showtime.MovieDetails.Title,
                showtime.MovieDetails.PosterUrl,
                showtime.MovieDetails.ReleaseYear),
            showtime.ScreeningTime.Value,
            showtime.AuditoriumId,
            showtime.Status.ToString(),
            showtime.CreatedAt);
}
