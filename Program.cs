using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;
using SocialCalc.Web.Data;
using SocialCalc.Web.Models;
using SocialCalc.Web.Services;
using SocialCalc.Web.Services.Pdf;
using Microsoft.AspNetCore.HttpOverrides;
using Serilog;
using Serilog.Events;
using Microsoft.AspNetCore.RateLimiting;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Http.Features;

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
    options.SignIn.RequireConfirmedEmail = false;
})
.AddEntityFrameworkStores<ApplicationDbContext>()
.AddDefaultTokenProviders();

builder.Services.Configure<DataProtectionTokenProviderOptions>(options =>
{
    options.TokenLifespan = TimeSpan.FromHours(24);
});

// ===== External Authentication =====
var clientId = builder.Configuration["Authentication:Google:ClientId"];
var clientSecret = builder.Configuration["Authentication:Google:ClientSecret"];

if (string.IsNullOrEmpty(clientId) || clientId == "YOUR_GOOGLE_CLIENT_ID_HERE" ||
    string.IsNullOrEmpty(clientSecret) || clientSecret == "YOUR_GOOGLE_CLIENT_SECRET_HERE")
{
    Log.Warning("Google OAuth credentials are not properly configured. Google login/export will not work.");
}
else
{
    builder.Services.AddAuthentication().AddGoogle(options =>
    {
        options.ClientId = clientId;
        options.ClientSecret = clientSecret;
        options.SaveTokens = true;
    });
}

// ===== Dependency Injection - Services =====
builder.Services.AddScoped<IAuthService, AuthService>();

builder.Services.AddScoped<ISheetService, SheetService>();
// Register Excel service implementation (choose PHP CLI or HTTP-based service via config)
if (builder.Configuration.GetValue<bool>("AppSettings:UsePhpCli"))
{
    builder.Services.AddScoped<ISpreadsheetService, PhpCliExcelService>();
}
else
{
    builder.Services.AddScoped<ISpreadsheetService, ExcelService>();
}
builder.Services.AddScoped<IPdfRenderEngine, PlaywrightRenderEngine>();
builder.Services.AddScoped<ISpreadsheetHtmlRenderer, SpreadsheetHtmlRenderer>();
builder.Services.AddSingleton<IPdfJobQueue, PdfJobQueue>();
builder.Services.AddScoped<IEmailService, EmailService>();

// ===== Background Services =====
builder.Services.AddHostedService<TempCleanupService>();
builder.Services.AddHostedService<PdfBackgroundWorker>();

// ===== HTTP Client for PHP Integration =====
builder.Services.AddHttpClient();

// ===== MVC & Razor Views =====
builder.Services.AddControllersWithViews();

// ===== Rate Limiting =====
builder.Services.AddRateLimiter(options =>
{
    options.AddPolicy("AuthPolicy", httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: httpContext.Connection.RemoteIpAddress?.ToString() ?? httpContext.Request.Headers.Host.ToString(),
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 5,
                Window = TimeSpan.FromMinutes(1),
                QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                QueueLimit = 0
            }));
            
    options.AddPolicy("ApiPolicy", httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: httpContext.Connection.RemoteIpAddress?.ToString() ?? httpContext.Request.Headers.Host.ToString(),
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 30,
                Window = TimeSpan.FromMinutes(1),
                QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                QueueLimit = 5
            }));
            
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
});

// ===== Form Options =====
builder.Services.Configure<FormOptions>(options =>
{
    options.MultipartBodyLengthLimit = 5_242_880; // 5MB
});

// ===== Configure Kestrel to listen on port 5000 =====
builder.WebHost.UseUrls("http://*:5000");

var app = builder.Build();

// Database migration removed from startup. Must be run via CI/CD.

// ===== Reverse Proxy configuration (Nginx) =====
var forwardedHeadersOptions = new ForwardedHeadersOptions
{
    ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto
};
forwardedHeadersOptions.KnownIPNetworks.Clear();
forwardedHeadersOptions.KnownProxies.Clear();
app.UseForwardedHeaders(forwardedHeadersOptions);



// ===== Security Headers Middleware =====
app.Use((context, next) =>
{
    context.Response.Headers.Append("X-Frame-Options", "DENY");
    context.Response.Headers.Append("X-Content-Type-Options", "nosniff");
    // Note: 'unsafe-eval' is required by SocialCalc to evaluate formulas. This is a known tradeoff.
    context.Response.Headers.Append("Content-Security-Policy", "default-src 'self'; script-src 'self' 'unsafe-inline' 'unsafe-eval' https://cdn.jsdelivr.net; style-src 'self' 'unsafe-inline' https://cdn.jsdelivr.net https://cdnjs.cloudflare.com; font-src 'self' https://cdnjs.cloudflare.com; img-src 'self' data:; connect-src 'self';");
    context.Response.Headers.Append("Referrer-Policy", "strict-origin-when-cross-origin");
    return next();
});

// ===== Middleware Pipeline =====
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
    app.UseHttpsRedirection();
}

app.UseStaticFiles();
app.UseRouting();

app.UseRateLimiter();

app.UseAuthentication();
app.UseAuthorization();

// ===== Route Configuration =====
app.MapControllers();
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

// Print a single blue listening line for operator visibility (keeps console minimal)
try
{
    var urls = string.Join(", ", app.Urls).Replace("0.0.0.0", "localhost");
    var previousColor = Console.ForegroundColor;
    Console.ForegroundColor = ConsoleColor.Blue;
    Console.WriteLine($"Now listening on: {urls} ");
    Console.ForegroundColor = previousColor;
}
catch (Exception ex)
{
    Log.Warning(ex, "Could not print startup URL.");
}

app.Run();


