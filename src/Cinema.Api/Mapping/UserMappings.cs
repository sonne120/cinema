using Cinema.Contracts.Authentication;
using Cinema.Domain.UserAggregate;

namespace Cinema.Api.Mapping;

public static class UserMappings
{
    public static AuthResponse ToAuthResponse(
        this User user,
        string accessToken,
        string refreshToken,
        DateTime expiresAt) =>
        new(user.Id.Value,
            user.Email,
            user.FirstName,
            user.LastName,
            user.Role.ToString(),
            accessToken,
            refreshToken,
            expiresAt);

    public static UserProfileResponse ToProfileResponse(this User user) =>
        new(user.Id.Value,
            user.Email,
            user.FirstName,
            user.LastName,
            user.FullName,
            user.Role.ToString(),
            user.CreatedAt,
            user.LastLoginAt);
}
