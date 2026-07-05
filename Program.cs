using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;
using SocialCalc.Web.Data;
using SocialCalc.Web.Models;
using SocialCalc.Web.Services;
using Serilog;
using Serilog.Events;

var builder = WebApplication.CreateBuilder(args);

// ===== Logging Configuration =====
Log.Logger = new LoggerConfiguration()
    // Keep file logs at Information for debugging/history
    .MinimumLevel.Information()
    // Reduce noisy framework logs
    .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
    .MinimumLevel.Override("Microsoft.AspNetCore", LogEventLevel.Warning)
    .MinimumLevel.Override("Microsoft.EntityFrameworkCore", LogEventLevel.Warning)
    // No console sink - console output suppressed
    .WriteTo.File("logs/app-.txt", rollingInterval: RollingInterval.Day)
    .CreateLogger();

builder.Host.UseSerilog();

// ===== Configuration =====
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection") 
    ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");

// ===== Database Configuration =====
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseNpgsql(connectionString)
);

// ===== Identity Configuration =====
builder.Services.AddIdentity<User, IdentityRole<int>>(options =>
{
    options.Password.RequireDigit = true;
    options.Password.RequiredLength = 8;
    options.Password.RequireNonAlphanumeric = false;
    options.Password.RequireUppercase = true;
    options.Password.RequireLowercase = true;
    options.User.RequireUniqueEmail = true;
    options.SignIn.RequireConfirmedEmail = true;
})
.AddEntityFrameworkStores<ApplicationDbContext>()
.AddDefaultTokenProviders();

// ===== Session Configuration =====
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromMinutes(30);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
});

// ===== Dependency Injection - Services =====
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IStorageService, StorageService>();
builder.Services.AddScoped<ISheetService, SheetService>();
// Register Excel service implementation (choose PHP CLI or HTTP-based service via config)
if (builder.Configuration.GetValue<bool>("AppSettings:UsePhpCli"))
{
    builder.Services.AddScoped<IExcelService, PhpCliExcelService>();
}
else
{
    builder.Services.AddScoped<IExcelService, ExcelService>();
}
builder.Services.AddScoped<IEmailService, EmailService>();

// ===== HTTP Client for PHP Integration =====
builder.Services.AddHttpClient();

// ===== MVC & Razor Views =====
builder.Services.AddControllersWithViews();

var app = builder.Build();

// ===== Middleware Pipeline =====
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

app.UseSession();

// ===== Route Configuration =====
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

// ===== Configure Kestrel to listen on 127.0.0.1:5000 =====
app.Urls.Clear();
app.Urls.Add("http://127.0.0.1:5000");

// ===== Database Migration =====
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    db.Database.Migrate();
    
    // Seed default users/data if needed
    var userManager = scope.ServiceProvider.GetRequiredService<UserManager<User>>();
    SeedDatabase(db, userManager);
}

    // Print a single blue listening line for operator visibility (keeps console minimal)
    try
    {
        var urls = string.Join(", ", app.Urls);
        var previousColor = Console.ForegroundColor;
        Console.ForegroundColor = ConsoleColor.Blue;
        Console.WriteLine($"Now listening on: {urls}");
        Console.ForegroundColor = previousColor;
    }
    catch { }

    app.Run();

// ===== Database Seeding =====
static void SeedDatabase(ApplicationDbContext context, UserManager<User> userManager)
{
    // Always ensure test user exists
    var testUserEmail = "demo@example.com";
    var existingUser = userManager.FindByEmailAsync(testUserEmail).Result;
    
    if (existingUser == null)
    {
        // Seed test user for demo purposes
        var testUser = new User
        {
            Email = testUserEmail,
            UserName = testUserEmail,
            EmailConfirmed = true,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };
        
        var result = userManager.CreateAsync(testUser, "DemoPass123").Result;
        
        // Re-fetch the user to get the auto-generated ID from database
        existingUser = userManager.FindByEmailAsync(testUserEmail).Result;
    }

    // NO AUTOMATIC SEEDING - Let user create their own sheets
}
