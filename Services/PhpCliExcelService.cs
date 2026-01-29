using System.Diagnostics;
using System.Text;
using SocialCalc.Web.Models;

namespace SocialCalc.Web.Services;

public class PhpCliExcelService : IExcelService
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<PhpCliExcelService> _logger;

    public PhpCliExcelService(IConfiguration configuration, ILogger<PhpCliExcelService> logger)
    {
        _configuration = configuration;
        _logger = logger;
    }

    public async Task<Stream?> ExportToExcelAsync(Sheet sheet)
    {
        return await ExportToFormatAsync(sheet, "Xlsx");
    }

    public async Task<Stream?> ExportToCsvAsync(Sheet sheet)
    {
        return await ExportToFormatAsync(sheet, "Csv");
    }

    public async Task<Stream?> ExportToFormatAsync(Sheet sheet, string format)
    {
        var phpPath = _configuration["AppSettings:PhpCliPath"] ?? "php";
        var scriptsDir = _configuration["AppSettings:PhpScriptsDir"] ?? "excelinterop";
        var tempDir = _configuration["AppSettings:PhpTempDir"] ?? Path.Combine(scriptsDir, "tmp");
        var timeoutSeconds = _configuration.GetValue<int?>("AppSettings:PhpCliTimeoutSeconds") ?? 30;

        Directory.CreateDirectory(tempDir);

        var extensions = new Dictionary<string, string>
        {
            { "Xlsx", ".xlsx" },
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
            // Debug: log the data being exported
            var dataPreview = sheet.Data?.Length > 500 ? sheet.Data.Substring(0, 500) : sheet.Data;
            _logger.LogInformation($"Export: Writing data to {inputFile}. Length={sheet.Data?.Length ?? 0}. Preview: {dataPreview}");
            
            // Use UTF8 without BOM - important for PHP json_decode to work correctly
            var utf8NoBom = new UTF8Encoding(false);
            await File.WriteAllTextAsync(inputFile, sheet.Data ?? string.Empty, utf8NoBom);

            var psi = new ProcessStartInfo
            {
                FileName = phpPath,
                Arguments = $"\"{scriptPath}\" \"{inputFile}\" \"{outputFile}\" {format}",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var proc = Process.Start(psi);
            if (proc == null)
            {
                _logger.LogError("Failed to start PHP process for export");
                return null;
            }

            var outputTask = proc.StandardOutput.ReadToEndAsync();
            var errTask = proc.StandardError.ReadToEndAsync();

            if (!proc.WaitForExit(timeoutSeconds * 1000))
            {
                try { proc.Kill(); } catch { }
                _logger.LogError("PHP export process timed out");
                return null;
            }

            var stdout = await outputTask;
            var stderr = await errTask;

            if (proc.ExitCode != 0)
            {
                _logger.LogError($"PHP export failed exitcode={proc.ExitCode} stdout={stdout} stderr={stderr}");
                return null;
            }

            if (!File.Exists(outputFile))
            {
                _logger.LogError($"PHP export did not produce output file: {outputFile}");
                return null;
            }

            var ms = new MemoryStream();
            using (var fs = File.OpenRead(outputFile))
            {
                await fs.CopyToAsync(ms);
            }
            ms.Seek(0, SeekOrigin.Begin);

            _logger.LogInformation($"Sheet exported to {format} (PHP CLI): {sheet.FileName}");
            return ms;
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error exporting to Excel via PHP CLI: {ex.Message}");
            return null;
        }
        finally
        {
            TryDelete(inputFile);
            TryDelete(outputFile);
        }
    }

    public async Task<Sheet?> ImportFromExcelAsync(Stream fileStream, int userId, string fileName)
    {
        var phpPath = _configuration["AppSettings:PhpCliPath"] ?? "php";
        var scriptsDir = _configuration["AppSettings:PhpScriptsDir"] ?? "excelinterop";
        var tempDir = _configuration["AppSettings:PhpTempDir"] ?? Path.Combine(scriptsDir, "tmp");
        var timeoutSeconds = _configuration.GetValue<int?>("AppSettings:PhpCliTimeoutSeconds") ?? 30;

        Directory.CreateDirectory(tempDir);

        var baseName = Path.Combine(tempDir, "tmp_" + Guid.NewGuid().ToString("N"));
        var inputFile = baseName + Path.GetExtension(fileName);
        var scriptPath = Path.Combine(scriptsDir, "import.php");

        try
        {
            using (var fs = File.Open(inputFile, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                await fileStream.CopyToAsync(fs);
            }

            var psi = new ProcessStartInfo
            {
                FileName = phpPath,
                Arguments = $"\"{scriptPath}\" \"{inputFile}\"",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var proc = Process.Start(psi);
            if (proc == null)
            {
                _logger.LogError("Failed to start PHP process for import");
                return null;
            }

            var outputTask = proc.StandardOutput.ReadToEndAsync();
            var errTask = proc.StandardError.ReadToEndAsync();

            if (!proc.WaitForExit(timeoutSeconds * 1000))
            {
                try { proc.Kill(); } catch { }
                _logger.LogError("PHP import process timed out");
                return null;
            }

            var stdout = await outputTask;
            var stderr = await errTask;

            if (proc.ExitCode != 0)
            {
                _logger.LogError($"PHP import failed exitcode={proc.ExitCode} stdout={stdout} stderr={stderr}");
                return null;
            }

            // CLI import.php prefixes output with "$---$" before the JSON
            var jsonIndex = stdout.IndexOf("$---$");
            var json = jsonIndex >= 0 ? stdout.Substring(jsonIndex + 5) : stdout;
            json = json.Trim();

            var sheet = new Sheet
            {
                UserId = userId,
                FileName = fileName,
                Data = json,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            _logger.LogInformation($"Sheet imported from Excel (PHP CLI): {fileName}");
            return sheet;
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error importing from Excel via PHP CLI: {ex.Message}");
            return null;
        }
        finally
        {
            TryDelete(inputFile);
        }
    }

    public async Task<bool> IsValidExcelFileAsync(Stream fileStream)
    {
        try
        {
            fileStream.Seek(0, SeekOrigin.Begin);
            var buffer = new byte[4];
            await fileStream.ReadAsync(buffer, 0, 4);

            var isXlsx = buffer[0] == 0x50 && buffer[1] == 0x4B;
            var isXls = buffer[0] == 0xD0 && buffer[1] == 0xCF;

            fileStream.Seek(0, SeekOrigin.Begin);
            return isXlsx || isXls;
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error validating Excel file: {ex.Message}");
            return false;
        }
    }

    private void TryDelete(string? path)
    {
        try
        {
            if (!string.IsNullOrEmpty(path) && File.Exists(path))
                File.Delete(path);
        }
        catch { }
    }
}