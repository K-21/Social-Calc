using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using SocialCalc.Web.Models;
using SocialCalc.Web.Services;
using Microsoft.AspNetCore.RateLimiting;
using System.ComponentModel.DataAnnotations;

namespace SocialCalc.Web.Controllers;

public class AuthController : Controller
{
    private readonly IAuthService _authService;
    private readonly IEmailService _emailService;
    private readonly SignInManager<User> _signInManager;
    private readonly ILogger<AuthController> _logger;

    public AuthController(
        IAuthService authService,
        IEmailService emailService,
        SignInManager<User> signInManager,
        ILogger<AuthController> logger)
    {
        _authService = authService;
        _emailService = emailService;
        _signInManager = signInManager;
        _logger = logger;
    }

    private static bool IsValidEmail(string email)
    {
        if (string.IsNullOrWhiteSpace(email)) return false;
        return new EmailAddressAttribute().IsValid(email);
    }

    [HttpGet("/login")]
    public IActionResult Login()
    {
        // Check if user is properly authenticated and has a valid user account
        if (User.Identity?.IsAuthenticated == true)
        {
            _logger.LogInformation("User already authenticated, redirecting to sheets");
            return RedirectToAction("Index", "Sheets");
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
            if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
            {
                ModelState.AddModelError("", "Email and password are required");
                return View();
            }

            var user = await _authService.AuthenticateUserAsync(email, password);
            if (user == null)
            {
                ModelState.AddModelError("", "Invalid email or password");
                return View();
            }

            await _signInManager.SignInAsync(user, isPersistent: true);
            await _authService.UpdateLastLoginAsync(user);

            _logger.LogInformation("User logged in: {UserId}", user.Id);
            return Redirect("/sheets");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Login error");
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
            if (!IsValidEmail(email))
            {
                ModelState.AddModelError("email", "A valid email is required");
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

            var result = await _authService.RegisterUserAsync(email, password);
            if (result.User == null)
            {
                foreach (var error in result.Errors)
                {
                    ModelState.AddModelError("", error);
                }
                return View();
            }

            // Send welcome email as fire-and-forget
            var userName = email.Contains('@') ? email.Split('@')[0] : email;
            _ = _emailService.SendWelcomeEmailAsync(email, userName);

            _logger.LogInformation("New user registered: {UserId}", result.User.Id);
            TempData["SuccessMessage"] = "Registration successful! Please log in.";
            return RedirectToAction("Login");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Registration error");
            ModelState.AddModelError("", "An error occurred during registration");
            return View();
        }
    }


    [HttpPost("/logout")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout()
    {
        await _signInManager.SignOutAsync();
        _logger.LogInformation("User logged out");
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
            if (!IsValidEmail(email))
            {
                ModelState.AddModelError("", "Please enter a valid email address");
                return View();
            }

            var user = await _authService.FindUserByEmailAsync(email);
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
            _logger.LogError(ex, "Forgot password error");
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
            if (string.IsNullOrWhiteSpace(token))
            {
                ModelState.AddModelError("", "Invalid or missing reset token.");
                return View();
            }
            
            if (!IsValidEmail(email))
            {
                ModelState.AddModelError("", "Please enter a valid email address");
                return View();
            }

            if (string.IsNullOrWhiteSpace(password))
            {
                ModelState.AddModelError("", "Password is required.");
                return View();
            }

            if (password != confirmPassword)
            {
                ModelState.AddModelError("", "Passwords do not match");
                return View();
            }

            var result = await _authService.ResetPasswordAsync(email, token, password);
            if (!result.Success)
            {
                foreach (var error in result.Errors)
                {
                    ModelState.AddModelError("", error);
                }
                return View();
            }

            TempData["SuccessMessage"] = "Password reset successful! Please log in.";
            return RedirectToAction("Login");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Reset password error");
            ModelState.AddModelError("", "An error occurred");
            return View();
        }
    }

}
