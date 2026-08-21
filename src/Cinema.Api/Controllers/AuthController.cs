using Cinema.Api.Mapping;
using Cinema.Application.Common.Interfaces.Authentication;
using Cinema.Contracts.Authentication;
using Cinema.Domain.UserAggregate;
using Cinema.Domain.UserAggregate.ValueObjects;
using Cinema.Infrastructure.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

// ControllerBase.User (the ClaimsPrincipal) shadows the domain type inside this class.
using DomainUser = Cinema.Domain.UserAggregate.User;

namespace Cinema.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private const string UserIdClaimType = "uid";

    private readonly IUserRepository _userRepository;
    private readonly IAuthService _authService;
    private readonly IPasswordHasher _passwordHasher;
    private readonly JwtSettings _jwtSettings;

    public AuthController(
        IUserRepository userRepository,
        IAuthService authService,
        IPasswordHasher passwordHasher,
        IOptions<JwtSettings> jwtSettings)
    {
        _userRepository = userRepository;
        _authService = authService;
        _passwordHasher = passwordHasher;
        _jwtSettings = jwtSettings.Value;
    }

    [HttpPost("register")]
    [ProducesResponseType(typeof(AuthResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Register(
        [FromBody] RegisterRequest request,
        CancellationToken cancellationToken)
    {
        if (await _userRepository.ExistsAsync(request.Email, cancellationToken))
        {
            return Conflict(new { message = "User with this email already exists" });
        }

        var passwordHash = _passwordHasher.Hash(request.Password);

        var userResult = DomainUser.Create(
            request.Email,
            passwordHash,
            request.FirstName,
            request.LastName);

        if (userResult.IsFailure)
        {
            return BadRequest(new { message = userResult.Error });
        }

        var user = userResult.Value;
        var response = IssueTokens(user);

        await _userRepository.AddAsync(user, cancellationToken);

        return CreatedAtAction(nameof(GetProfile), new { id = user.Id.Value }, response);
    }

    [HttpPost("login")]
    [ProducesResponseType(typeof(AuthResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Login(
        [FromBody] LoginRequest request,
        CancellationToken cancellationToken)
    {
        var user = await _userRepository.GetByEmailAsync(request.Email, cancellationToken);

        if (user == null)
        {
            return Unauthorized(new { message = "Invalid email or password" });
        }

        if (!user.IsActive)
        {
            return Unauthorized(new { message = "Account is deactivated" });
        }

        if (!_passwordHasher.Verify(request.Password, user.PasswordHash))
        {
            return Unauthorized(new { message = "Invalid email or password" });
        }

        var response = IssueTokens(user);
        user.RecordLogin();

        await _userRepository.UpdateAsync(user, cancellationToken);

        return Ok(response);
    }

    [HttpPost("refresh")]
    [ProducesResponseType(typeof(AuthResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> RefreshToken(
        [FromBody] RefreshTokenRequest request,
        CancellationToken cancellationToken)
    {
        var userId = GetUserIdFromExpiredToken(request.AccessToken);
        if (userId == null)
        {
            return Unauthorized(new { message = "Invalid token" });
        }

        var user = await _userRepository.GetByIdAsync(UserId.Create(userId.Value), cancellationToken);

        if (user == null || !user.IsRefreshTokenValid(request.RefreshToken))
        {
            return Unauthorized(new { message = "Invalid refresh token" });
        }

        var response = IssueTokens(user);
        await _userRepository.UpdateAsync(user, cancellationToken);

        return Ok(response);
    }

    [Authorize]
    [HttpPost("logout")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Logout(CancellationToken cancellationToken)
    {
        var user = await GetCurrentUserAsync(cancellationToken);

        if (user != null)
        {
            user.RevokeRefreshToken();
            await _userRepository.UpdateAsync(user, cancellationToken);
        }

        return NoContent();
    }

    [Authorize]
    [HttpGet("profile")]
    [ProducesResponseType(typeof(UserProfileResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetProfile(CancellationToken cancellationToken)
    {
        var user = await GetCurrentUserAsync(cancellationToken);
        if (user == null)
        {
            return NotFound();
        }

        return Ok(user.ToProfileResponse());
    }

    [Authorize]
    [HttpPut("profile")]
    [ProducesResponseType(typeof(UserProfileResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateProfile(
        [FromBody] UpdateProfileRequest request,
        CancellationToken cancellationToken)
    {
        var user = await GetCurrentUserAsync(cancellationToken);
        if (user == null)
        {
            return NotFound();
        }

        var result = user.UpdateProfile(request.FirstName, request.LastName);
        if (result.IsFailure)
        {
            return BadRequest(new { message = result.Error });
        }

        await _userRepository.UpdateAsync(user, cancellationToken);

        return Ok(user.ToProfileResponse());
    }

    [Authorize]
    [HttpPost("change-password")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ChangePassword(
        [FromBody] ChangePasswordRequest request,
        CancellationToken cancellationToken)
    {
        var user = await GetCurrentUserAsync(cancellationToken);
        if (user == null)
        {
            return NotFound();
        }

        if (!_passwordHasher.Verify(request.CurrentPassword, user.PasswordHash))
        {
            return BadRequest(new { message = "Current password is incorrect" });
        }

        var newPasswordHash = _passwordHasher.Hash(request.NewPassword);
        user.ChangePassword(newPasswordHash);
        user.RevokeRefreshToken();

        await _userRepository.UpdateAsync(user, cancellationToken);

        return NoContent();
    }

    /// <summary>
    /// Issues a fresh access/refresh token pair and stores the refresh token on the user.
    /// The caller is responsible for persisting the user afterwards.
    /// </summary>
    private AuthResponse IssueTokens(DomainUser user)
    {
        var accessToken = _authService.GenerateAccessToken(user);
        var refreshToken = _authService.GenerateRefreshToken();

        user.SetRefreshToken(refreshToken, DateTime.UtcNow.AddDays(_jwtSettings.RefreshTokenExpirationDays));

        return user.ToAuthResponse(
            accessToken,
            refreshToken,
            DateTime.UtcNow.AddMinutes(_jwtSettings.AccessTokenExpirationMinutes));
    }

    /// <summary>
    /// Resolves the authenticated caller, or null when the token carries no usable
    /// user id or the user no longer exists.
    /// </summary>
    private async Task<DomainUser?> GetCurrentUserAsync(CancellationToken cancellationToken)
    {
        var userIdClaim = User.FindFirst(UserIdClaimType)?.Value;

        if (string.IsNullOrEmpty(userIdClaim) || !Guid.TryParse(userIdClaim, out var userId))
        {
            return null;
        }

        return await _userRepository.GetByIdAsync(UserId.Create(userId), cancellationToken);
    }

    private static Guid? GetUserIdFromExpiredToken(string token)
    {
        try
        {
            var jwtToken = new System.IdentityModel.Tokens.Jwt.JwtSecurityTokenHandler().ReadJwtToken(token);
            var userIdClaim = jwtToken.Claims.FirstOrDefault(c => c.Type == UserIdClaimType)?.Value;

            if (Guid.TryParse(userIdClaim, out var userId))
            {
                return userId;
            }
        }
        catch
        {
            // A malformed token is simply an unauthenticated caller.
        }

        return null;
    }
}
