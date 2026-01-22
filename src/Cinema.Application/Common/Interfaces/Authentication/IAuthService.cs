using Cinema.Domain.UserAggregate;

namespace Cinema.Application.Common.Interfaces.Authentication;

public interface IAuthService
{
    string GenerateAccessToken(User user);
    string GenerateRefreshToken();
    bool ValidateRefreshToken(User user, string refreshToken);
}

public interface IPasswordHasher
{
    string Hash(string password);
    bool Verify(string password, string hash);
}
