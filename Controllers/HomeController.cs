using Microsoft.AspNetCore.Mvc;
using SocialCalc.Web.Data;

namespace SocialCalc.Web.Controllers;

public class HomeController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<HomeController> _logger;

    public HomeController(
        ApplicationDbContext context,
        ILogger<HomeController> logger)
    {
        _context = context;
        _logger = logger;
    }

    public IActionResult Index()
    {
        // Redirect to login if not authenticated
        if (!User.Identity?.IsAuthenticated ?? true)
        {
            return RedirectToAction("Login", "Auth");
        }

        return RedirectToAction("Index", "Sheets");
    }

    public IActionResult Privacy()
    {
        return View();
    }

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View();
    }
}
