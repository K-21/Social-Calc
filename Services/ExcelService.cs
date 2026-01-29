using SocialCalc.Web.Models;

namespace SocialCalc.Web.Services;

public class ExcelService : IExcelService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IConfiguration _configuration;
    private readonly ILogger<ExcelService> _logger;

    public ExcelService(
        IHttpClientFactory httpClientFactory,
        IConfiguration configuration,
        ILogger<ExcelService> logger)
    {
        _httpClientFactory = httpClientFactory;
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
        try
        {
            var client = _httpClientFactory.CreateClient();
            var phpServiceUrl = _configuration["AppSettings:PhpServiceUrl"];

            if (string.IsNullOrEmpty(phpServiceUrl))
            {
                _logger.LogError("PHP service URL not configured");
                return null;
            }

            var exportUrl = $"{phpServiceUrl}export.php?format={format.ToLower()}";
            var content = new StringContent(sheet.Data, System.Text.Encoding.UTF8, "application/json");

            var response = await client.PostAsync(exportUrl, content);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError($"PHP export to {format} failed: {response.StatusCode}");
                return null;
            }

            var stream = await response.Content.ReadAsStreamAsync();
            _logger.LogInformation($"Sheet exported to {format}: {sheet.FileName}");
            return stream;
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error exporting to {format}: {ex.Message}");
            return null;
        }
    }

    public async Task<Sheet?> ImportFromExcelAsync(Stream fileStream, int userId, string fileName)
    {
        try
        {
            if (!IsValidExcelFileAsync(fileStream).Result)
            {
                _logger.LogWarning($"Invalid Excel file: {fileName}");
                return null;
            }

            var client = _httpClientFactory.CreateClient();
            var phpServiceUrl = _configuration["AppSettings:PhpServiceUrl"];

            if (string.IsNullOrEmpty(phpServiceUrl))
            {
                _logger.LogError("PHP service URL not configured");
                return null;
            }

            var importUrl = $"{phpServiceUrl}import.php";
            
            var content = new MultipartFormDataContent();
            content.Add(new StreamContent(fileStream), "file", fileName);
            content.Add(new StringContent(userId.ToString()), "userId");

            var response = await client.PostAsync(importUrl, content);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError($"PHP import failed: {response.StatusCode}");
                return null;
            }

            var jsonData = await response.Content.ReadAsStringAsync();
            
            var sheet = new Sheet
            {
                UserId = userId,
                FileName = fileName,
                Data = jsonData,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            _logger.LogInformation($"Sheet imported from Excel: {fileName}");
            return sheet;
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error importing from Excel: {ex.Message}");
            return null;
        }
    }

    public async Task<bool> IsValidExcelFileAsync(Stream fileStream)
    {
        try
        {
            fileStream.Seek(0, SeekOrigin.Begin);
            var buffer = new byte[4];
            await fileStream.ReadAsync(buffer, 0, 4);

            // Check for Excel file signatures
            // .xlsx files start with PK (0x50 0x4B)
            // .xls files start with D0 CF (0xD0 0xCF)
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
}
