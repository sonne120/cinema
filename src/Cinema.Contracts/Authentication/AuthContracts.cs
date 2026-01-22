namespace Cinema.Contracts.Authentication;

public record RegisterRequest(
    string Email,
    string Password,
    string FirstName,
    string LastName);

public record LoginRequest(
    string Email,
    string Password);

public record RefreshTokenRequest(
    string AccessToken,
    string RefreshToken);

public record AuthResponse(
    Guid UserId,
    string Email,
    string FirstName,
    string LastName,
    string Role,
    string AccessToken,
    string RefreshToken,
    DateTime ExpiresAt);

public record ChangePasswordRequest(
    string CurrentPassword,
    string NewPassword);

public record UpdateProfileRequest(
    string FirstName,
    string LastName);
