using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using SocialCalc.Web.Models;
using SocialCalc.Web.Services;

namespace SocialCalc.Web.Controllers;

[Authorize]
public class ProfileController : Controller
{
    private readonly UserManager<User> _userManager;
    private readonly IAuthService _authService;

    public ProfileController(UserManager<User> userManager, IAuthService authService)
    {
        _userManager = userManager;
        _authService = authService;
    }

    public async Task<IActionResult> Index()
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null) return Challenge();

        var tokens = await _authService.GetUserApiTokensAsync(user.Id);
        return View(tokens);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> GenerateToken(string tokenName)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null) return Challenge();

        var (rawToken, dbToken) = await _authService.GenerateApiTokenAsync(user.Id, tokenName);
        
        TempData["NewToken"] = rawToken; // Display exactly once
        TempData["SuccessMessage"] = "Token generated successfully! Please copy it now, as you won't be able to see it again.";
        
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RevokeToken(int tokenId)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null) return Challenge();

        var result = await _authService.RevokeApiTokenAsync(tokenId, user.Id);
        if (result)
        {
            TempData["SuccessMessage"] = "Token revoked successfully.";
        }
        else
        {
            TempData["ErrorMessage"] = "Failed to revoke token.";
        }

        return RedirectToAction(nameof(Index));
    }
}
