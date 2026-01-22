using Cinema.Application.Common.Interfaces.Authentication;

namespace Cinema.Infrastructure.Authentication;

public class PasswordHasher : IPasswordHasher
{
    public string Hash(string password)
    {
        return BCrypt.Net.BCrypt.HashPassword(password, BCrypt.Net.BCrypt.GenerateSalt(12));
    }

    public bool Verify(string password, string hash)
    {
        return BCrypt.Net.BCrypt.Verify(password, hash);
    }
}
