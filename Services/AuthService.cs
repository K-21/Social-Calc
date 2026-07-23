using BCrypt.Net;
using Microsoft.AspNetCore.Identity;
using SocialCalc.Web.Data;
using SocialCalc.Web.Models;
using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;

namespace SocialCalc.Web.Services;

public class AuthService : IAuthService
{
    private readonly ApplicationDbContext _context;
    private readonly UserManager<User> _userManager;
    private readonly ILogger<AuthService> _logger;

    public AuthService(
        ApplicationDbContext context,
        UserManager<User> userManager,
        ILogger<AuthService> logger)
    {
        _context = context;
        _userManager = userManager;
        _logger = logger;
    }

    public async Task<User?> AuthenticateUserAsync(string email, string password)
    {
        try
        {
            var user = await _userManager.FindByEmailAsync(email);
            if (user == null)
            {
                _logger.LogWarning($"Login attempt for non-existent user: {email}");
                return null;
            }

            if (!user.IsActive)
            {
                _logger.LogWarning($"Login attempt for inactive user: {email}");
                return null;
            }

            var result = await _userManager.CheckPasswordAsync(user, password);
            if (!result)
            {
                _logger.LogWarning($"Failed login attempt for user: {email}");
                return null;
            }

            return user;
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error during authentication: {ex.Message}");
            return null;
        }
    }

    public async Task<bool> RegisterUserAsync(string email, string password)
    {
        try
        {
            var existingUser = await _userManager.FindByEmailAsync(email);
            if (existingUser != null)
            {
                _logger.LogWarning($"Registration attempt with existing email: {email}");
                return false;
            }

            var user = new User
            {
                Email = email,
                UserName = email,
                EmailConfirmed = false,
                CreatedAt = DateTime.UtcNow
            };

            var result = await _userManager.CreateAsync(user, password);
            if (!result.Succeeded)
            {
                var errors = string.Join(", ", result.Errors.Select(e => e.Description));
                _logger.LogError($"Registration failed for {email}: {errors}");
                return false;
            }

            _logger.LogInformation($"New user registered: {email}");
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error during registration: {ex.Message}");
            return false;
        }
    }

    public async Task<bool> ValidateUserAsync(User user)
    {
        try
        {
            return user != null && user.IsActive && user.EmailConfirmed;
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error validating user: {ex.Message}");
            return false;
        }
    }

    public async Task<string> GeneratePasswordResetTokenAsync(User user)
    {
        try
        {
            var resetToken = await _userManager.GeneratePasswordResetTokenAsync(user);
            
            var token = new PasswordResetToken
            {
                UserId = user.Id,
                Token = resetToken,
                CreatedAt = DateTime.UtcNow,
                ExpiresAt = DateTime.UtcNow.AddHours(24)
            };

            _context.PasswordResetTokens.Add(token);
            await _context.SaveChangesAsync();

            _logger.LogInformation($"Password reset token generated for user: {user.Email}");
            return resetToken;
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error generating reset token: {ex.Message}");
            return string.Empty;
        }
    }

    public async Task<bool> ResetPasswordAsync(string token, string newPassword)
    {
        try
        {
            var resetToken = await _context.PasswordResetTokens
                .FirstOrDefaultAsync(t => t.Token == token && !t.IsUsed && t.ExpiresAt > DateTime.UtcNow);

            if (resetToken == null)
            {
                _logger.LogWarning("Invalid or expired password reset token");
                return false;
            }

            var user = await _userManager.FindByIdAsync(resetToken.UserId.ToString());
            if (user == null)
            {
                return false;
            }

            var result = await _userManager.ResetPasswordAsync(user, token, newPassword);
            if (!result.Succeeded)
            {
                var errors = string.Join(", ", result.Errors.Select(e => e.Description));
                _logger.LogError($"Password reset failed: {errors}");
                return false;
            }

            // Clean up used token and any other expired tokens
            _context.PasswordResetTokens.Remove(resetToken);
            
            var expiredTokens = await _context.PasswordResetTokens
                .Where(t => t.ExpiresAt <= DateTime.UtcNow || t.IsUsed)
                .ToListAsync();
                
            if (expiredTokens.Any())
            {
                _context.PasswordResetTokens.RemoveRange(expiredTokens);
            }
            
            await _context.SaveChangesAsync();

            _logger.LogInformation($"Password reset successful for user: {user.Email}");
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error resetting password: {ex.Message}");
            return false;
        }
    }

    public async Task UpdateLastLoginAsync(User user)
    {
        try
        {
            user.LastLogin = DateTime.UtcNow;
            await _userManager.UpdateAsync(user);
            _logger.LogInformation($"Last login updated for user: {user.Email}");
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error updating last login: {ex.Message}");
        }
    }

    public async Task<(string rawToken, PersonalAccessToken dbToken)> GenerateApiTokenAsync(int userId, string name)
    {
        // 1. Generate secure random string
        var rawToken = "sc_" + Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))
            .Replace("+", "-")
            .Replace("/", "_")
            .Replace("=", "");

        // 2. Hash it for DB storage
        var tokenHash = ComputeSha256Hash(rawToken);

        var dbToken = new PersonalAccessToken
        {
            UserId = userId,
            Name = string.IsNullOrWhiteSpace(name) ? "API Token" : name,
            TokenHash = tokenHash,
            CreatedAt = DateTime.UtcNow
        };

        _context.PersonalAccessTokens.Add(dbToken);
        await _context.SaveChangesAsync();

        return (rawToken, dbToken);
    }

    public async Task<int?> ValidateApiTokenAsync(string rawToken)
    {
        if (string.IsNullOrWhiteSpace(rawToken))
            return null;

        var tokenHash = ComputeSha256Hash(rawToken);

        var dbToken = await _context.PersonalAccessTokens
            .FirstOrDefaultAsync(t => t.TokenHash == tokenHash);

        if (dbToken == null)
            return null;

        // Update LastUsedAt in background (fire & forget style)
        dbToken.LastUsedAt = DateTime.UtcNow;
        _context.PersonalAccessTokens.Update(dbToken);
        await _context.SaveChangesAsync();

        return dbToken.UserId;
    }

    public async Task<List<PersonalAccessToken>> GetUserApiTokensAsync(int userId)
    {
        return await _context.PersonalAccessTokens
            .Where(t => t.UserId == userId)
            .OrderByDescending(t => t.CreatedAt)
            .ToListAsync();
    }

    public async Task<bool> RevokeApiTokenAsync(int tokenId, int userId)
    {
        var token = await _context.PersonalAccessTokens
            .FirstOrDefaultAsync(t => t.Id == tokenId && t.UserId == userId);

        if (token == null)
            return false;

        _context.PersonalAccessTokens.Remove(token);
        await _context.SaveChangesAsync();
        return true;
    }

    private static string ComputeSha256Hash(string rawData)
    {
        using (SHA256 sha256Hash = SHA256.Create())
        {
            byte[] bytes = sha256Hash.ComputeHash(System.Text.Encoding.UTF8.GetBytes(rawData));
            var builder = new System.Text.StringBuilder();
            for (int i = 0; i < bytes.Length; i++)
            {
                builder.Append(bytes[i].ToString("x2"));
            }
            return builder.ToString();
        }
    }
}
