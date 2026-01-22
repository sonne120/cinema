using Cinema.Domain.Common.Models;
using Cinema.Domain.UserAggregate.ValueObjects;

namespace Cinema.Domain.UserAggregate;

public sealed class User : AggregateRoot<UserId>
{
    public string Email { get; private set; }
    public string PasswordHash { get; private set; }
    public string FirstName { get; private set; }
    public string LastName { get; private set; }
    public UserRole Role { get; private set; }
    public bool IsActive { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime? LastLoginAt { get; private set; }
    public string? RefreshToken { get; private set; }
    public DateTime? RefreshTokenExpiryTime { get; private set; }

    private User() : base(UserId.CreateUnique())
    {
        Email = string.Empty;
        PasswordHash = string.Empty;
        FirstName = string.Empty;
        LastName = string.Empty;
    }

    private User(
        UserId id,
        string email,
        string passwordHash,
        string firstName,
        string lastName,
        UserRole role) : base(id)
    {
        Email = email;
        PasswordHash = passwordHash;
        FirstName = firstName;
        LastName = lastName;
        Role = role;
        IsActive = true;
        CreatedAt = DateTime.UtcNow;
    }

    public static Result<User> Create(
        string email,
        string passwordHash,
        string firstName,
        string lastName,
        UserRole role = UserRole.Customer)
    {
        if (string.IsNullOrWhiteSpace(email))
            return Result.Failure<User>("Email is required");

        if (!IsValidEmail(email))
            return Result.Failure<User>("Invalid email format");

        if (string.IsNullOrWhiteSpace(passwordHash))
            return Result.Failure<User>("Password is required");

        if (string.IsNullOrWhiteSpace(firstName))
            return Result.Failure<User>("First name is required");

        if (string.IsNullOrWhiteSpace(lastName))
            return Result.Failure<User>("Last name is required");

        var userId = UserId.CreateUnique();
        var user = new User(userId, email.ToLowerInvariant(), passwordHash, firstName, lastName, role);

        return Result.Success(user);
    }

    public string FullName => $"{FirstName} {LastName}";

    public Result UpdateProfile(string firstName, string lastName)
    {
        if (string.IsNullOrWhiteSpace(firstName))
            return Result.Failure("First name is required");

        if (string.IsNullOrWhiteSpace(lastName))
            return Result.Failure("Last name is required");

        FirstName = firstName;
        LastName = lastName;
        return Result.Success();
    }

    public Result ChangePassword(string newPasswordHash)
    {
        if (string.IsNullOrWhiteSpace(newPasswordHash))
            return Result.Failure("Password is required");

        PasswordHash = newPasswordHash;
        return Result.Success();
    }

    public Result ChangeRole(UserRole newRole)
    {
        Role = newRole;
        return Result.Success();
    }

    public void RecordLogin()
    {
        LastLoginAt = DateTime.UtcNow;
    }

    public void SetRefreshToken(string token, DateTime expiryTime)
    {
        RefreshToken = token;
        RefreshTokenExpiryTime = expiryTime;
    }

    public void RevokeRefreshToken()
    {
        RefreshToken = null;
        RefreshTokenExpiryTime = null;
    }

    public bool IsRefreshTokenValid(string token)
    {
        return RefreshToken == token &&
               RefreshTokenExpiryTime.HasValue &&
               RefreshTokenExpiryTime.Value > DateTime.UtcNow;
    }

    public Result Deactivate()
    {
        if (!IsActive)
            return Result.Failure("User is already deactivated");

        IsActive = false;
        RevokeRefreshToken();
        return Result.Success();
    }

    public Result Activate()
    {
        if (IsActive)
            return Result.Failure("User is already active");

        IsActive = true;
        return Result.Success();
    }

    private static bool IsValidEmail(string email)
    {
        try
        {
            var addr = new System.Net.Mail.MailAddress(email);
            return addr.Address == email;
        }
        catch
        {
            return false;
        }
    }
}
