using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using Microsoft.AspNetCore.Hosting;

namespace SocialCalc.Web.Services;

public class TempCleanupService : BackgroundService
{
    private readonly ILogger<TempCleanupService> _logger;
    private readonly string _tempPath;

    public TempCleanupService(
        ILogger<TempCleanupService> logger,
        IConfiguration configuration,
        IWebHostEnvironment env)
    {
        _logger = logger;
        var scriptsDir = configuration["AppSettings:PhpScriptsDir"] ?? "excelinterop";
        var tempDir = configuration["AppSettings:PhpTempDir"] ?? Path.Combine(scriptsDir, "tmp");
        
        // Check if absolute path or relative
        if (Path.IsPathRooted(tempDir))
        {
            _tempPath = tempDir;
        }
        else
        {
            _tempPath = Path.Combine(env.ContentRootPath, tempDir);
        }
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Temp cleanup service is starting. Target: {TempPath}", _tempPath);

        // Run every hour
        using PeriodicTimer timer = new PeriodicTimer(TimeSpan.FromHours(1));

        try
        {
            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                await Task.Run(() => CleanupOldTempFiles());
            }
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Temp cleanup service is stopping.");
        }
    }

    private void CleanupOldTempFiles()
    {
        try
        {
            if (!Directory.Exists(_tempPath))
            {
                return;
            }

            var oneDayAgo = DateTime.UtcNow.AddHours(-24);
            int deletedCount = 0;

            foreach (var file in Directory.GetFiles(_tempPath))
            {
                if (File.GetLastWriteTimeUtc(file) < oneDayAgo)
                {
                    try
                    {
                        File.Delete(file);
                        deletedCount++;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to delete temp file: {File}", file);
                    }
                }
            }

            if (deletedCount > 0)
            {
                _logger.LogInformation("Cleaned up {Count} old temp files from {Path}", deletedCount, _tempPath);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error occurred while cleaning up temp files.");
        }
    }


}
