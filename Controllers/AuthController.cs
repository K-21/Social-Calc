using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using SocialCalc.Web.Models;
using SocialCalc.Web.Services;
using System.Security.Claims;
using Microsoft.AspNetCore.RateLimiting;

namespace SocialCalc.Web.Controllers;

public class AuthController : Controller
{
    private readonly IAuthService _authService;
    private readonly IEmailService _emailService;
    private readonly SignInManager<User> _signInManager;
    private readonly UserManager<User> _userManager;
    private readonly ILogger<AuthController> _logger;

    public AuthController(
        IAuthService authService,
        IEmailService emailService,
        SignInManager<User> signInManager,
        UserManager<User> userManager,
        ILogger<AuthController> logger)
    {
        _authService = authService;
        _emailService = emailService;
        _signInManager = signInManager;
        _userManager = userManager;
        _logger = logger;
    }

    [HttpGet("/login")]
    public async Task<IActionResult> Login()
    {
        // Check if user is properly authenticated and has a valid user account
        if (User.Identity?.IsAuthenticated == true)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user != null && user.IsActive)
            {
                _logger.LogInformation($"User {user.Email} already authenticated, redirecting to sheets");
                return RedirectToAction("Index", "Sheets");
            }
            else
            {
                // User is "authenticated" but user record doesn't exist or is inactive - sign out
                _logger.LogWarning("Invalid user session detected, signing out");
                await _signInManager.SignOutAsync();
            }
        }
        
        return View();
    }

    [HttpPost("/login")]
    [EnableRateLimiting("AuthPolicy")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(string email, string password)
    {
        try
        {
            var user = await _authService.AuthenticateUserAsync(email, password);
            if (user == null)
            {
                ModelState.AddModelError("", "Invalid email or password");
                return View();
            }

            await _signInManager.SignInAsync(user, isPersistent: true);
            await _authService.UpdateLastLoginAsync(user);

            _logger.LogInformation($"User logged in: {email}");
            return Redirect("/sheets");
        }
        catch (Exception ex)
        {
            _logger.LogError($"Login error: {ex.Message}");
            ModelState.AddModelError("", "An error occurred during login");
            return View();
        }
    }

    [HttpGet("/register")]
    public IActionResult Register()
    {
        if (User.Identity?.IsAuthenticated ?? false)
        {
            return RedirectToAction("Index", "Sheets");
        }
        return View();
    }

    [HttpPost("/register")]
    [EnableRateLimiting("AuthPolicy")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Register(string email, string password, string confirmPassword)
    {
        try
        {
            // Validate input
            if (string.IsNullOrWhiteSpace(email))
            {
                ModelState.AddModelError("email", "Email is required");
                return View();
            }

            if (string.IsNullOrWhiteSpace(password))
            {
                ModelState.AddModelError("password", "Password is required");
                return View();
            }

            if (password != confirmPassword)
            {
                ModelState.AddModelError("confirmPassword", "Passwords do not match");
                return View();
            }

            if (password.Length < 8)
            {
                ModelState.AddModelError("password", "Password must be at least 8 characters long");
                return View();
            }

            var success = await _authService.RegisterUserAsync(email, password);
            if (!success)
            {
                ModelState.AddModelError("", "Registration failed. This email may already be in use or password doesn't meet requirements (needs uppercase and number).");
                return View();
            }

            var user = await _userManager.FindByEmailAsync(email);
            if (user != null)
            {
                await _emailService.SendWelcomeEmailAsync(email, email);
                // Note: In production, implement email confirmation
                user.EmailConfirmed = true;
                await _userManager.UpdateAsync(user);
            }

            _logger.LogInformation($"New user registered: {email}");
            TempData["SuccessMessage"] = "Registration successful! Please log in.";
            return RedirectToAction("Login");
        }
        catch (Exception ex)
        {
            _logger.LogError($"Registration error: {ex.Message}");
            ModelState.AddModelError("", "An error occurred during registration");
            return View();
        }
    }


    [HttpPost("/logout")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout()
    {
        await _signInManager.SignOutAsync();
        _logger.LogInformation($"User logged out");
        return RedirectToAction("Login");
    }

    [HttpGet("/lostpw")]
    public IActionResult ForgotPassword()
    {
        return View();
    }

    [HttpPost("/lostpw")]
    [EnableRateLimiting("AuthPolicy")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ForgotPassword(string email)
    {
        try
        {
            var user = await _userManager.FindByEmailAsync(email);
            if (user == null)
            {
                // Don't reveal if user exists (security best practice)
                TempData["SuccessMessage"] = "If the email exists, a reset link has been sent.";
                return RedirectToAction("Login");
            }

            var token = await _authService.GeneratePasswordResetTokenAsync(user);
            var resetLink = Url.Action("ResetPassword", "Auth",
                new { token = token, email = email }, Request.Scheme);

            await _emailService.SendPasswordResetEmailAsync(email, resetLink ?? "");

            TempData["SuccessMessage"] = "If the email exists, a reset link has been sent.";
            return RedirectToAction("Login");
        }
        catch (Exception ex)
        {
            _logger.LogError($"Forgot password error: {ex.Message}");
            ModelState.AddModelError("", "An error occurred");
            return View();
        }
    }

    [HttpGet("/pwreset")]
    public IActionResult ResetPassword(string token, string email)
    {
        if (string.IsNullOrEmpty(token) || string.IsNullOrEmpty(email))
        {
            return RedirectToAction("Login");
        }
        return View();
    }

    [HttpPost("/pwreset")]
    [EnableRateLimiting("AuthPolicy")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ResetPassword(string token, string email, string password, string confirmPassword)
    {
        try
        {
            if (password != confirmPassword)
            {
                ModelState.AddModelError("", "Passwords do not match");
                return View();
            }

            var success = await _authService.ResetPasswordAsync(token, password);
            if (!success)
            {
                ModelState.AddModelError("", "Password reset failed. Token may be expired.");
                return View();
            }

            TempData["SuccessMessage"] = "Password reset successful! Please log in.";
            return RedirectToAction("Login");
        }
        catch (Exception ex)
        {
            _logger.LogError($"Reset password error: {ex.Message}");
            ModelState.AddModelError("", "An error occurred");
            return View();
        }
    }

}
