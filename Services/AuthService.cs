using Microsoft.AspNetCore.Identity;
using SocialCalc.Web.Models;

namespace SocialCalc.Web.Services;

public class AuthService : IAuthService
{
    private readonly UserManager<User> _userManager;
    private readonly ILogger<AuthService> _logger;

    public AuthService(
        UserManager<User> userManager,
        ILogger<AuthService> logger)
    {
        _userManager = userManager;
        _logger = logger;
    }

    public async Task<User?> AuthenticateUserAsync(string email, string password)
    {
        try
        {
            var user = await FindUserByEmailAsync(email);
            if (user == null)
            {
                _logger.LogWarning("Login attempt for non-existent user");
                return null;
            }

            if (!user.IsActive)
            {
                _logger.LogWarning("Login attempt for inactive user");
                return null;
            }

            var result = await _userManager.CheckPasswordAsync(user, password);
            if (!result)
            {
                _logger.LogWarning("Failed login attempt");
                return null;
            }

            return user;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during authentication");
            return null;
        }
    }

    public async Task<(User? User, IEnumerable<string> Errors)> RegisterUserAsync(string email, string password)
    {
        try
        {
            var existingUser = await _userManager.FindByEmailAsync(email);
            if (existingUser != null)
            {
                _logger.LogWarning("Registration attempt with existing email");
                return (null, new[] { "Email is already in use" });
            }

            var user = new User
            {
                Email = email,
                UserName = email,
                EmailConfirmed = true,
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            };

            var result = await _userManager.CreateAsync(user, password);
            if (!result.Succeeded)
            {
                var errors = string.Join(", ", result.Errors.Select(e => e.Description));
                _logger.LogError("Registration failed: {Errors}", errors);
                return (null, result.Errors.Select(e => e.Description));
            }

            _logger.LogInformation("New user registered");
            return (user, Enumerable.Empty<string>());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during registration");
            return (null, new[] { "An unexpected error occurred during registration" });
        }
    }

    public Task<bool> ValidateUserAsync(User user)
    {
        try
        {
            return Task.FromResult(user != null && user.IsActive && user.EmailConfirmed);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error validating user");
            return Task.FromResult(false);
        }
    }

    public async Task<string> GeneratePasswordResetTokenAsync(User user)
    {
        try
        {
            var resetToken = await _userManager.GeneratePasswordResetTokenAsync(user);
            _logger.LogInformation("Password reset token generated for user: {UserId}", user.Id);
            return resetToken;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating reset token");
            return string.Empty;
        }
    }

    public async Task<(bool Success, IEnumerable<string> Errors)> ResetPasswordAsync(string email, string token, string newPassword)
    {
        try
        {
            var user = await FindUserByEmailAsync(email);
            if (user == null)
            {
                _logger.LogWarning("Invalid user or email mismatch for reset token");
                return (false, new[] { "Invalid reset token." });
            }

            var result = await _userManager.ResetPasswordAsync(user, token, newPassword);
            if (!result.Succeeded)
            {
                var errors = string.Join(", ", result.Errors.Select(e => e.Description));
                _logger.LogError("Password reset failed: {Errors}", errors);
                return (false, result.Errors.Select(e => e.Description));
            }

            _logger.LogInformation("Password reset successful for user: {Email}", user.Email);
            return (true, Enumerable.Empty<string>());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error resetting password");
            return (false, new[] { "An unexpected error occurred." });
        }
    }

    public async Task UpdateLastLoginAsync(User user)
    {
        try
        {
            user.LastLogin = DateTime.UtcNow;
            await _userManager.UpdateAsync(user);
            _logger.LogInformation("Last login updated for user: {Email}", user.Email);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating last login");
        }
    }

    public async Task<User?> FindUserByEmailAsync(string email)
    {
        try
        {
            return await _userManager.FindByEmailAsync(email);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error finding user by email");
            return null;
        }
    }
}
