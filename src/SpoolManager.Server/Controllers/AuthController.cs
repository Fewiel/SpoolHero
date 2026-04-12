using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SpoolManager.Infrastructure.Data;
using SpoolManager.Infrastructure.Repositories;
using SpoolManager.Infrastructure.Services;
using SpoolManager.Server.Services;
using SpoolManager.Shared.DTOs.Auth;

namespace SpoolManager.Server.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    private readonly IUserRepository _users;
    private readonly IJwtTokenService _jwt;
    private readonly IAuditService _audit;
    private readonly LoginRateLimiter _rateLimiter;
    private readonly IEmailService _email;

    public AuthController(IUserRepository users, IJwtTokenService jwt, IAuditService audit,
        LoginRateLimiter rateLimiter, IEmailService email)
    {
        _users = users;
        _jwt = jwt;
        _audit = audit;
        _rateLimiter = rateLimiter;
        _email = email;
    }

    private string? ClientIp => HttpContext.Connection.RemoteIpAddress?.ToString();

    [HttpPost("register")]
    public async Task<IActionResult> Register(RegisterRequest request)
    {
        if (await _users.ExistsAsync(request.Email, request.Username))
            return Conflict(new { message = "Email or username already taken." });

        var emailEnabled = await _email.IsEnabledAsync();

        var user = new AppUser
        {
            Username = request.Username,
            Email = request.Email,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password),
            EmailVerified = !emailEnabled
        };

        if (emailEnabled)
        {
            user.EmailVerificationToken = GenerateToken();
            user.EmailVerificationTokenExpires = DateTime.UtcNow.AddHours(24);
        }

        await _users.CreateAsync(user);

        if (emailEnabled)
            await _email.SendVerificationEmailAsync(user);

        await _audit.LogAsync("auth.register",
            username: request.Username,
            entityType: "user", entityName: request.Username,
            ipAddress: ClientIp);

        return Ok(new { message = "Registration successful.", requiresEmailVerification = emailEnabled });
    }

    [HttpGet("verify-email")]
    public async Task<IActionResult> VerifyEmail([FromQuery] string token)
    {
        if (string.IsNullOrWhiteSpace(token)) return BadRequest(new { message = "Token required." });
        var user = await _users.GetByVerificationTokenAsync(token);
        if (user == null) return BadRequest(new { message = "Invalid or expired token." });
        if (user.EmailVerificationTokenExpires < DateTime.UtcNow)
            return BadRequest(new { message = "Token expired." });

        user.EmailVerified = true;
        user.EmailVerificationToken = null;
        user.EmailVerificationTokenExpires = null;
        await _users.UpdateAsync(user);

        return Ok(new { message = "Email verified." });
    }

    [HttpPost("resend-verification")]
    public async Task<IActionResult> ResendVerification([FromBody] ResendVerificationRequest request)
    {
        var ipKey = $"resend:{ClientIp}";
        if (_rateLimiter.IsBlocked(ipKey))
            return StatusCode(429, new { message = "Too many requests. Please try again later." });

        var user = await _users.GetByEmailAsync(request.Email);
        if (user == null || user.EmailVerified) return Ok();

        if (user.EmailVerificationTokenExpires.HasValue &&
            user.EmailVerificationTokenExpires.Value > DateTime.UtcNow.AddMinutes(-2))
        {
            _rateLimiter.RecordFailure(ipKey);
            return Ok(new { message = "Verification email sent." });
        }

        user.EmailVerificationToken = GenerateToken();
        user.EmailVerificationTokenExpires = DateTime.UtcNow.AddHours(24);
        await _users.UpdateAsync(user);
        await _email.SendVerificationEmailAsync(user);

        return Ok(new { message = "Verification email sent." });
    }

    [HttpPost("forgot-password")]
    public async Task<IActionResult> ForgotPassword([FromBody] ForgotPasswordRequest request)
    {
        var ipKey = $"forgot:{ClientIp}";
        if (_rateLimiter.IsBlocked(ipKey))
            return StatusCode(429, new { message = "Too many requests. Please try again later." });

        var user = await _users.GetByEmailAsync(request.Email);
        if (user != null)
        {
            if (user.PasswordResetTokenExpires.HasValue &&
                user.PasswordResetTokenExpires.Value > DateTime.UtcNow.AddMinutes(58))
            {
                return Ok(new { message = "If the email exists, a reset link has been sent." });
            }

            user.PasswordResetToken = GenerateToken();
            user.PasswordResetTokenExpires = DateTime.UtcNow.AddHours(1);
            await _users.UpdateAsync(user);
            await _email.SendPasswordResetEmailAsync(user);

            await _audit.LogAsync("auth.password.reset.request", username: request.Email,
                entityType: "user", entityName: request.Email, ipAddress: ClientIp);
        }

        _rateLimiter.RecordFailure(ipKey);
        return Ok(new { message = "If the email exists, a reset link has been sent." });
    }

    [HttpPost("reset-password")]
    public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordRequest request)
    {
        var ipKey = $"reset:{ClientIp}";
        if (_rateLimiter.IsBlocked(ipKey))
            return StatusCode(429, new { message = "Too many requests. Please try again later." });

        if (string.IsNullOrWhiteSpace(request.Token)) return BadRequest(new { message = "Token required." });
        if (string.IsNullOrWhiteSpace(request.NewPassword) || request.NewPassword.Length < 8)
            return BadRequest(new { message = "Password must be at least 8 characters." });

        var user = await _users.GetByResetTokenAsync(request.Token);
        if (user == null)
        {
            _rateLimiter.RecordFailure(ipKey);
            return BadRequest(new { message = "Invalid or expired token." });
        }
        if (user.PasswordResetTokenExpires < DateTime.UtcNow)
        {
            _rateLimiter.RecordFailure(ipKey);
            return BadRequest(new { message = "Token expired." });
        }

        user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.NewPassword);
        user.PasswordResetToken = null;
        user.PasswordResetTokenExpires = null;
        user.TokenVersion++;
        await _users.UpdateAsync(user);

        await _audit.LogAsync("auth.password.reset.complete", userId: user.Id, username: user.Username,
            entityType: "user", entityId: user.Id.ToString(), entityName: user.Username,
            ipAddress: ClientIp);

        return Ok(new { message = "Password has been reset." });
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login(LoginRequest request)
    {
        var rateLimitKey = request.Email.ToLowerInvariant();

        if (_rateLimiter.IsBlocked(rateLimitKey))
        {
            var remaining = _rateLimiter.GetRemainingLockout(rateLimitKey);
            var minutes = remaining.HasValue ? (int)Math.Ceiling(remaining.Value.TotalMinutes) : 15;
            await _audit.LogAsync("auth.login.blocked", username: request.Email,
                details: $"Rate limited – {minutes} min remaining", ipAddress: ClientIp);
            return StatusCode(429, new { message = $"Too many failed attempts. Try again in {minutes} minutes." });
        }

        var user = await _users.GetByEmailAsync(request.Email);
        if (user == null || !BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash))
        {
            _rateLimiter.RecordFailure(rateLimitKey);
            await _audit.LogAsync("auth.login.fail", username: request.Email,
                details: "Invalid credentials", ipAddress: ClientIp);
            return Unauthorized(new { message = "Invalid email or password." });
        }

        if (!user.EmailVerified && user.EmailVerificationToken != null)
            return Unauthorized(new { message = "email_not_verified" });

        _rateLimiter.RecordSuccess(rateLimitKey);
        var token = _jwt.GenerateToken(user);

        await _audit.LogAsync("auth.login.success", userId: user.Id, username: user.Username,
            entityType: "user", entityId: user.Id.ToString(), entityName: user.Username,
            ipAddress: ClientIp);

        return Ok(new LoginResponse
        {
            AccessToken = token,
            ExpiresIn = 86400,
            Username = user.Username,
            Email = user.Email
        });
    }

    [Authorize]
    [HttpPut("password")]
    public async Task<IActionResult> ChangePassword(ChangePasswordRequest request)
    {
        var idClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (!Guid.TryParse(idClaim, out var userId)) return Unauthorized();

        var user = await _users.GetByIdAsync(userId);
        if (user == null) return NotFound();

        if (!BCrypt.Net.BCrypt.Verify(request.CurrentPassword, user.PasswordHash))
            return BadRequest(new { message = "Current password is incorrect." });

        user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.NewPassword);
        user.TokenVersion++;
        await _users.UpdateAsync(user);

        var newToken = _jwt.GenerateToken(user);
        await _audit.LogAsync("auth.password.change", userId: user.Id, username: user.Username,
            entityType: "user", entityId: user.Id.ToString(), entityName: user.Username,
            ipAddress: ClientIp);

        return Ok(new { accessToken = newToken, message = "Password changed successfully." });
    }

    [Authorize]
    [HttpGet("me")]
    public async Task<IActionResult> Me()
    {
        var idClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (!Guid.TryParse(idClaim, out var userId)) return Unauthorized();

        var user = await _users.GetByIdAsync(userId);
        if (user == null) return NotFound();

        return Ok(new UserDto
        {
            Id = user.Id,
            Username = user.Username,
            Email = user.Email,
            IsPlatformAdmin = user.IsPlatformAdmin,
            IsSuperAdmin = user.IsSuperAdmin,
            DashboardOnboardingDismissed = user.DashboardOnboardingDismissed,
            CreatedAt = user.CreatedAt
        });
    }

    [Authorize]
    [HttpPut("dashboard-onboarding")]
    public async Task<IActionResult> SetDashboardOnboarding([FromBody] SetDashboardOnboardingRequest request)
    {
        var idClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (!Guid.TryParse(idClaim, out var userId)) return Unauthorized();

        var user = await _users.GetByIdAsync(userId);
        if (user == null) return NotFound();

        user.DashboardOnboardingDismissed = request.Dismissed;
        await _users.UpdateAsync(user);
        return NoContent();
    }

    [Authorize]
    [HttpGet("notifications")]
    public async Task<IActionResult> GetNotificationPrefs()
    {
        var idClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (!Guid.TryParse(idClaim, out var userId)) return Unauthorized();
        var user = await _users.GetByIdAsync(userId);
        if (user == null) return NotFound();
        return Ok(new { notifySpoolLow = user.NotifySpoolLow, notifyDryerDone = user.NotifyDryerDone, notifyTicketReply = user.NotifyTicketReply });
    }

    [Authorize]
    [HttpPut("notifications")]
    public async Task<IActionResult> SaveNotificationPrefs([FromBody] SpoolManager.Shared.DTOs.Admin.NotificationPrefsDto prefs)
    {
        var idClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (!Guid.TryParse(idClaim, out var userId)) return Unauthorized();
        var user = await _users.GetByIdAsync(userId);
        if (user == null) return NotFound();
        user.NotifySpoolLow = prefs.NotifySpoolLow;
        user.NotifyDryerDone = prefs.NotifyDryerDone;
        user.NotifyTicketReply = prefs.NotifyTicketReply;
        await _users.UpdateAsync(user);
        return NoContent();
    }

    [Authorize]
    [HttpPut("language")]
    public async Task<IActionResult> SetLanguage([FromBody] SetLanguageRequest request)
    {
        var idClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (!Guid.TryParse(idClaim, out var userId)) return Unauthorized();
        var user = await _users.GetByIdAsync(userId);
        if (user == null) return NotFound();
        var lang = request.Language?.ToLowerInvariant();
        if (lang != "de" && lang != "en") return BadRequest();
        user.PreferredLanguage = lang;
        await _users.UpdateAsync(user);
        return NoContent();
    }

    [Authorize]
    [HttpDelete("account")]
    public async Task<IActionResult> DeleteAccount([FromBody] DeleteAccountRequest request)
    {
        var idClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (!Guid.TryParse(idClaim, out var userId)) return Unauthorized();

        var user = await _users.GetByIdAsync(userId);
        if (user == null) return NotFound();

        if (user.IsSuperAdmin)
            return BadRequest(new { message = "The main admin account cannot be deleted." });

        if (!BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash))
            return BadRequest(new { message = "Incorrect password." });

        await _audit.LogAsync("auth.account.delete", userId: userId, username: user.Username,
            entityType: "user", entityId: userId.ToString(), entityName: user.Username,
            ipAddress: ClientIp);

        await _users.DeleteAsync(userId);
        return NoContent();
    }

    private static string GenerateToken() =>
        Convert.ToHexString(System.Security.Cryptography.RandomNumberGenerator.GetBytes(32));
}
