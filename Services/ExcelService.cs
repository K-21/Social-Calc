using SocialCalc.Web.Models;

namespace SocialCalc.Web.Services;

public class ExcelService : SpreadsheetServiceBase
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IConfiguration _configuration;

    public ExcelService(
        IHttpClientFactory httpClientFactory,
        IConfiguration configuration,
        ILogger<ExcelService> logger) : base(logger)
    {
        _httpClientFactory = httpClientFactory;
        _configuration = configuration;
    }

    public override async Task<byte[]> ExportAsync(SpreadsheetData data, string format)
    {
        try
        {
            var client = _httpClientFactory.CreateClient();
            var phpServiceUrl = _configuration["AppSettings:PhpServiceUrl"];

            if (string.IsNullOrEmpty(phpServiceUrl))
            {
                _logger.LogError("PHP service URL not configured");
                return Array.Empty<byte>();
            }

            var exportUrl = $"{phpServiceUrl}export.php?format={Uri.EscapeDataString(format.ToLower())}";
            var content = new StringContent(data.JsonData ?? "{}", System.Text.Encoding.UTF8, "application/json");

            var response = await client.PostAsync(exportUrl, content);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("PHP export to {Format} failed: {StatusCode}", format, response.StatusCode);
                return Array.Empty<byte>();
            }

            var bytes = await response.Content.ReadAsByteArrayAsync();
            _logger.LogInformation("Sheet exported to {Format}: {FileName}", format, data.FileName);
            return bytes;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error exporting to {Format}", format);
            return Array.Empty<byte>();
        }
    }

    public override async Task<SpreadsheetData> ImportAsync(Stream fileStream, string format)
    {
        try
        {
            var client = _httpClientFactory.CreateClient();
            var phpServiceUrl = _configuration["AppSettings:PhpServiceUrl"];

            if (string.IsNullOrEmpty(phpServiceUrl))
            {
                _logger.LogError("PHP service URL not configured");
                return new SpreadsheetData();
            }

            var importUrl = $"{phpServiceUrl}import.php";
            
            var content = new MultipartFormDataContent();
            var formatExt = string.IsNullOrEmpty(format) ? ".xlsx" : (format.StartsWith(".") ? format : "." + format);
            content.Add(new StreamContent(fileStream), "file", "import" + formatExt);

            var response = await client.PostAsync(importUrl, content);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("PHP import failed: {StatusCode}", response.StatusCode);
                return new SpreadsheetData();
            }

            var jsonData = await response.Content.ReadAsStringAsync();
            
            var data = new SpreadsheetData
            {
                FileName = "Imported",
                JsonData = jsonData
            };

            return data;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error importing from Excel");
            return new SpreadsheetData();
        }
    }

}
