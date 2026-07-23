using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SocialCalc.Web.Services;

namespace SocialCalc.Web.Integrations.GoogleWorkspace
{
    [ApiController]
    [Route("api/gworkspace")]
    public class GoogleWorkspaceController : ControllerBase
    {
        private readonly IExcelService _excelService;
        private readonly ISheetService _sheetService;
        private readonly IConfiguration _configuration;
        private readonly ILogger<GoogleWorkspaceController> _logger;

        public GoogleWorkspaceController(
            IExcelService excelService,
            ISheetService sheetService,
            IConfiguration configuration,
            ILogger<GoogleWorkspaceController> logger)
        {
            _excelService = excelService;
            _sheetService = sheetService;
            _configuration = configuration;
            _logger = logger;
        }

        [HttpPost("import")]
        [AllowAnonymous] // We handle custom API Key auth manually
        public async Task<IActionResult> GoogleWorkspaceImport([FromBody] GWorkspaceImportRequest request, [FromHeader(Name = "X-Api-Key")] string apiKey)
        {
            try
            {
                // 1. Authenticate Request
                var configuredApiKey = _configuration["GoogleWorkspace:ApiKey"];
                if (string.IsNullOrEmpty(configuredApiKey) || apiKey != configuredApiKey)
                {
                    _logger.LogWarning("Unauthorized Google Workspace import attempt.");
                    return Unauthorized(new { success = false, message = "Invalid API Key" });
                }

                // 2. Validate Data
                if (request == null || request.Data == null || request.Data.Count == 0)
                {
                    return BadRequest(new { success = false, message = "No data provided" });
                }

                // 3. Convert 2D Array to CSV string
                var sb = new System.Text.StringBuilder();
                foreach (var row in request.Data)
                {
                    var escapedValues = row.Select(val =>
                    {
                        var s = val?.ToString() ?? "";
                        if (s.Contains(",") || s.Contains("\"") || s.Contains("\n"))
                        {
                            return $"\"{s.Replace("\"", "\"\"")}\"";
                        }
                        return s;
                    });
                    sb.AppendLine(string.Join(",", escapedValues));
                }

                // 4. Create MemoryStream from CSV
                var csvBytes = System.Text.Encoding.UTF8.GetBytes(sb.ToString());
                using var stream = new MemoryStream(csvBytes);
                var fileName = string.IsNullOrWhiteSpace(request.SheetName) ? "ImportedSheet.csv" : $"{request.SheetName}.csv";

                // Determine user - either link to a specific system user or default admin.
                // For this implementation, we will use a configured default user ID or first user.
                var defaultUserIdStr = _configuration["GoogleWorkspace:DefaultUserId"];
                int userId = 1; // Fallback
                if (int.TryParse(defaultUserIdStr, out int parsedId))
                {
                    userId = parsedId;
                }

                // 5. Use existing ExcelService to process the CSV
                var sheet = await _excelService.ImportFromExcelAsync(stream, userId, fileName);
                if (sheet == null)
                {
                    return StatusCode(500, new { success = false, message = "Error processing data" });
                }

                // 6. Save the generated SocialCalc sheet to DB
                var savedSheet = await _sheetService.SaveSheetAsync(userId, sheet.FileName, sheet.Data);
                if (savedSheet == null)
                {
                    return StatusCode(500, new { success = false, message = "Error saving imported sheet" });
                }

                _logger.LogInformation($"GWorkspace Sheet imported and saved: {savedSheet.FileName} (ID: {savedSheet.Id})");
                return Ok(new { success = true, id = savedSheet.Id, message = "Sheet imported successfully" });
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error in Google Workspace import: {ex.Message}");
                return StatusCode(500, new { success = false, message = "Server error processing import" });
            }
        }
    }

    public class GWorkspaceImportRequest
    {
        public string SheetName { get; set; } = "";
        public List<List<object>> Data { get; set; } = new();
        public int RowCount { get; set; }
        public int ColCount { get; set; }
    }
}
