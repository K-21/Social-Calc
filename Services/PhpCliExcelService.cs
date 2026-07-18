using System.Diagnostics;
using System.Text;
using SocialCalc.Web.Models;

namespace SocialCalc.Web.Services;

public class PhpCliExcelService : SpreadsheetServiceBase
{
    private readonly IConfiguration _configuration;

    public PhpCliExcelService(IConfiguration configuration, ILogger<PhpCliExcelService> logger) : base(logger)
    {
        _configuration = configuration;
    }

    public override async Task<byte[]> ExportAsync(SpreadsheetData data, string format)
    {
        var phpPath = _configuration["AppSettings:PhpCliPath"] ?? "php";
        var scriptsDir = _configuration["AppSettings:PhpScriptsDir"] ?? "excelinterop";
        var tempDir = _configuration["AppSettings:PhpTempDir"] ?? Path.Combine(scriptsDir, "tmp");
        var timeoutSeconds = _configuration.GetValue<int?>("AppSettings:PhpCliTimeoutSeconds") ?? 30;

        var allowedFormats = new[] { "Xlsx", "Xls", "Csv", "Html", "Ods", "Pdf" };
        if (!allowedFormats.Contains(format, StringComparer.OrdinalIgnoreCase))
        {
            _logger.LogError("Invalid format specified for export: {Format}", format);
            return Array.Empty<byte>();
        }

        Directory.CreateDirectory(tempDir);

        var extensions = new Dictionary<string, string>
        {
            { "Xlsx", ".xlsx" },
            { "Xls", ".xls" },
            { "Csv", ".csv" },
            { "Html", ".html" },
            { "Ods", ".ods" },
            { "Pdf", ".pdf" }
        };

        var baseName = Path.Combine(tempDir, "tmp_" + Guid.NewGuid().ToString("N"));
        var inputFile = baseName + ".b";
        var outputFile = baseName + extensions.GetValueOrDefault(format, ".xlsx");
        var scriptPath = Path.Combine(scriptsDir, "export.php");

        try
        {
            _logger.LogInformation("Export: Writing data for sheet {FileName}. Length={Length}", data.FileName, data.JsonData?.Length ?? 0);
            
            var utf8NoBom = new UTF8Encoding(false);
            await File.WriteAllTextAsync(inputFile, data.JsonData ?? string.Empty, utf8NoBom);



            var safeFileName = data.FileName?.Replace("\"", "\\\"") ?? "Exported";
            var psi = new ProcessStartInfo
            {
                FileName = phpPath,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            psi.ArgumentList.Add(scriptPath);
            psi.ArgumentList.Add(inputFile);
            psi.ArgumentList.Add(outputFile);
            psi.ArgumentList.Add(format);
            psi.ArgumentList.Add(safeFileName);

            using var proc = Process.Start(psi);
            if (proc == null)
            {
                _logger.LogError("Failed to start PHP process for export");
                return Array.Empty<byte>();
            }

            var outputTask = proc.StandardOutput.ReadToEndAsync();
            var errTask = proc.StandardError.ReadToEndAsync();

            using var cts = new CancellationTokenSource(timeoutSeconds * 1000);
            try
            {
                await proc.WaitForExitAsync(cts.Token);
            }
            catch (TaskCanceledException)
            {
                try { proc.Kill(); } catch { }
                _logger.LogError("PHP export process timed out");
                return Array.Empty<byte>();
            }

            var stdout = await outputTask;
            var stderr = await errTask;

            if (proc.ExitCode != 0)
            {
                _logger.LogError("PHP export failed. ExitCode: {ExitCode}, Stderr: {Stderr}", proc.ExitCode, stderr);
                return Array.Empty<byte>();
            }

            if (!File.Exists(outputFile))
            {
                _logger.LogError("PHP export succeeded but output file not found");
                return Array.Empty<byte>();
            }

            var result = await File.ReadAllBytesAsync(outputFile);
            _logger.LogInformation("Export finished for {FileName}. Size: {Size} bytes", data.FileName, result.Length);
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error exporting to Excel via PHP CLI");
            return Array.Empty<byte>();
        }
        finally
        {
            TryDelete(inputFile);
            TryDelete(outputFile);
        }
    }

    public override async Task<SpreadsheetData> ImportAsync(Stream fileStream, string format)
    {
        var phpPath = _configuration["AppSettings:PhpCliPath"] ?? "php";
        var scriptsDir = _configuration["AppSettings:PhpScriptsDir"] ?? "excelinterop";
        var tempDir = _configuration["AppSettings:PhpTempDir"] ?? Path.Combine(scriptsDir, "tmp");
        var timeoutSeconds = _configuration.GetValue<int?>("AppSettings:PhpCliTimeoutSeconds") ?? 30;

        Directory.CreateDirectory(tempDir);

        var baseName = Path.Combine(tempDir, "tmp_" + Guid.NewGuid().ToString("N"));
        var formatExt = string.IsNullOrEmpty(format) ? ".xlsx" : (format.StartsWith(".") ? format : "." + format);
        var inputFile = baseName + formatExt;
        var outputFile = baseName + ".json";
        var scriptPath = Path.Combine(scriptsDir, "import.php");

        try
        {
            // Write input stream to temp file
            using (var fs = new FileStream(inputFile, FileMode.Create, FileAccess.Write, FileShare.None, 4096, true))
            {
                await fileStream.CopyToAsync(fs);
            }

            var psi = new ProcessStartInfo
            {
                FileName = phpPath,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            psi.ArgumentList.Add(scriptPath);
            psi.ArgumentList.Add(inputFile);
            psi.ArgumentList.Add(outputFile);

            using var proc = Process.Start(psi);
            if (proc == null)
            {
                _logger.LogError("Failed to start PHP process for import");
                return new SpreadsheetData();
            }

            var outputTask = proc.StandardOutput.ReadToEndAsync();
            var errTask = proc.StandardError.ReadToEndAsync();

            using var cts = new CancellationTokenSource(timeoutSeconds * 1000);
            try
            {
                await proc.WaitForExitAsync(cts.Token);
            }
            catch (TaskCanceledException)
            {
                try { proc.Kill(); } catch { }
                _logger.LogError("PHP import process timed out");
                return new SpreadsheetData();
            }

            var stdout = await outputTask;
            var stderr = await errTask;

            if (proc.ExitCode != 0)
            {
                _logger.LogError("PHP import failed. ExitCode: {ExitCode}, Stderr: {Stderr}", proc.ExitCode, stderr);
                return new SpreadsheetData();
            }

            var json = await File.ReadAllTextAsync(outputFile);
            
            var result = new SpreadsheetData
            {
                JsonData = json,
                FileName = "Imported"
            };

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error importing from Excel via PHP CLI");
            return new SpreadsheetData();
        }
        finally
        {
            TryDelete(inputFile);
            TryDelete(outputFile);
        }
    }

    private void TryDelete(string? path)
    {
        try
        {
            if (!string.IsNullOrEmpty(path) && File.Exists(path))
                File.Delete(path);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to delete temp file {Path}", path);
        }
    }
}