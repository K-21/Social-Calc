using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;
using SocialCalc.Web.Data;
using SocialCalc.Web.Models;
using SocialCalc.Web.Services;
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

// ===== External Authentication =====
builder.Services.AddAuthentication()
    .AddGoogle(options =>
    {
        options.ClientId = builder.Configuration["Authentication:Google:ClientId"] ?? "";
        options.ClientSecret = builder.Configuration["Authentication:Google:ClientSecret"] ?? "";
        options.SaveTokens = true; // Save the access token to the authentication cookie
        options.Scope.Add("https://www.googleapis.com/auth/drive.file");
        // We removed the manual prompt addition to fix the OAuth error
    });



// ===== Dependency Injection - Services =====
builder.Services.AddScoped<IAuthService, AuthService>();

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

// ===== Rate Limiting =====
builder.Services.AddRateLimiter(options =>
{
    options.AddFixedWindowLimiter("AuthPolicy", opt =>
    {
        opt.PermitLimit = 5;
        opt.Window = TimeSpan.FromMinutes(1);
        opt.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
        opt.QueueLimit = 0;
    });
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
});

// ===== Form Options =====
builder.Services.Configure<FormOptions>(options =>
{
    options.MultipartBodyLengthLimit = 5_242_880; // 5MB
});

var app = builder.Build();

// ===== Reverse Proxy configuration (Nginx) =====
var forwardedHeadersOptions = new ForwardedHeadersOptions
{
    ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto
};
forwardedHeadersOptions.KnownNetworks.Clear();
forwardedHeadersOptions.KnownProxies.Clear();
app.UseForwardedHeaders(forwardedHeadersOptions);

app.Use((context, next) =>
{
    if (!app.Environment.IsDevelopment())
    {
        context.Request.Scheme = "https";
        var publicHostname = builder.Configuration["AppSettings:PublicHostname"] ?? "social-calc.duckdns.org";
        context.Request.Host = new HostString(publicHostname);
    }
    return next();
});

// ===== Security Headers Middleware =====
app.Use((context, next) =>
{
    context.Response.Headers.Append("X-Frame-Options", "DENY");
    context.Response.Headers.Append("X-Content-Type-Options", "nosniff");
    context.Response.Headers.Append("Content-Security-Policy", "default-src 'self'; script-src 'self' 'unsafe-inline' 'unsafe-eval' https://cdn.jsdelivr.net; style-src 'self' 'unsafe-inline' https://cdn.jsdelivr.net https://cdnjs.cloudflare.com; font-src 'self' https://cdnjs.cloudflare.com; img-src 'self' data:; connect-src 'self';");
    context.Response.Headers.Append("Referrer-Policy", "strict-origin-when-cross-origin");
    return next();
});

// ===== Middleware Pipeline =====
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();

app.UseRateLimiter();

app.UseAuthentication();
app.UseAuthorization();

// ===== Route Configuration =====
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

// ===== Configure Kestrel to listen on 127.0.0.1:5000 =====
app.Urls.Clear();
app.Urls.Add("http://0.0.0.0:5000");

// ===== Database Migration =====
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    db.Database.Migrate();
    
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

// ===== Temp File Cleanup =====
try
{
    var tmpDir = Path.Combine(builder.Environment.ContentRootPath, "excelinterop", "tmp");
    if (Directory.Exists(tmpDir))
    {
        var oneHourAgo = DateTime.UtcNow.AddHours(-1);
        foreach (var file in Directory.GetFiles(tmpDir))
        {
            if (File.GetCreationTimeUtc(file) < oneHourAgo)
            {
                File.Delete(file);
            }
        }
    }
}
catch { }

app.Run();

