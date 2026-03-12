using System.ComponentModel.DataAnnotations;

namespace SpoolManager.Shared.DTOs.Auth;

public class LoginRequest
{
    [Required, EmailAddress, MaxLength(256)]
    public string Email { get; set; } = string.Empty;

    [Required, MinLength(1), MaxLength(256)]
    public string Password { get; set; } = string.Empty;
}

public class LoginResponse
{
    public string AccessToken { get; set; } = string.Empty;
    public int ExpiresIn { get; set; }
    public string Username { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
}

public class RegisterRequest
{
    [Required, MinLength(3), MaxLength(100)]
    public string Username { get; set; } = string.Empty;

    [Required, EmailAddress, MaxLength(256)]
    public string Email { get; set; } = string.Empty;

    [Required, MinLength(8), MaxLength(256)]
    public string Password { get; set; } = string.Empty;
}

public class ResendVerificationRequest
{
    [Required, EmailAddress]
    public string Email { get; set; } = string.Empty;
}

public class ChangePasswordRequest
{
    [Required, MinLength(1), MaxLength(256)]
    public string CurrentPassword { get; set; } = string.Empty;

    [Required, MinLength(8), MaxLength(256)]
    public string NewPassword { get; set; } = string.Empty;
}

public class DeleteAccountRequest
{
    [Required, MinLength(1), MaxLength(256)]
    public string Password { get; set; } = string.Empty;
}

public class SetLanguageRequest
{
    public string Language { get; set; } = "de";
}

public class UserDto
{
    public Guid Id { get; set; }
    public string Username { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public bool IsPlatformAdmin { get; set; }
    public bool IsSuperAdmin { get; set; }
    public DateTime CreatedAt { get; set; }
}
