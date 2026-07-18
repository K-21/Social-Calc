using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using SocialCalc.Web.Models;
using SocialCalc.Web.Services;
using System.Text.Json;
using Microsoft.AspNetCore.Identity;
using Google.Apis.Auth.OAuth2;
using Google.Apis.Drive.v3;
using Google.Apis.Services;
using GoogleFile = Google.Apis.Drive.v3.Data.File;

namespace SocialCalc.Web.Controllers;

[Authorize]
[Route("/sheets")]
public class SheetsController : Controller
{
    private readonly ISheetService _sheetService;
    private readonly ISpreadsheetService _spreadsheetService;
    private readonly ILogger<SheetsController> _logger;
    private readonly IAuthenticationSchemeProvider _schemeProvider;
    private readonly IConfiguration _configuration;

    public SheetsController(
        ISheetService sheetService,
        ISpreadsheetService spreadsheetService,
        ILogger<SheetsController> logger,
        IAuthenticationSchemeProvider schemeProvider,
        IConfiguration configuration)
    {
        _sheetService = sheetService;
        _spreadsheetService = spreadsheetService;
        _logger = logger;
        _schemeProvider = schemeProvider;
        _configuration = configuration;
    }

    private bool TryGetCurrentUserId(out int userId)
    {
        userId = 0;
        var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return !string.IsNullOrEmpty(userIdStr) && int.TryParse(userIdStr, out userId);
    }

    [HttpGet("")]
    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public async Task<IActionResult> Index(int page = 1)
    {
        try
        {
            if (!TryGetCurrentUserId(out int userId))
            {
                return RedirectToAction("Login", "Auth");
            }

            int pageSize = _configuration.GetValue<int>("AppSettings:DashboardPageSize", 10);
            var totalSheets = await _sheetService.GetTotalUserSheetsAsync(userId);
            var sheets = await _sheetService.GetUserSheetsAsync(userId, page, pageSize);

            ViewBag.CurrentPage = page;
            ViewBag.TotalPages = (int)Math.Ceiling((double)totalSheets / pageSize);
            
            return View(sheets);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving sheets");
            return View(new List<Sheet>());
        }
    }

    [HttpGet("{id}")]
    [HttpGet("Editor/{id}")]
    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public async Task<IActionResult> Editor(int id)
    {
        try
        {
            if (!TryGetCurrentUserId(out int userId))
            {
                return RedirectToAction("Login", "Auth");
            }

            var sheet = await _sheetService.GetSheetAsync(id, userId);
            if (sheet == null)
            {
                return NotFound();
            }

            return View(sheet);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading sheet editor");
            return NotFound();
        }
    }

    [HttpPost("save/{id}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Save(int id, [FromBody] SaveSheetRequest request)
    {
        try
        {
            if (request == null || request.Data == null)
            {
                return BadRequest(new { success = false, message = "Invalid request data" });
            }

            if (!TryGetCurrentUserId(out int userId))
            {
                return Unauthorized();
            }

            var sheet = await _sheetService.GetSheetAsync(id, userId);
            if (sheet == null)
            {
                return NotFound();
            }

            // Log what we received
            _logger.LogInformation("Save request received. Data length: {DataLength}", request.Data?.Length ?? 0);

            // Data is already a string from JavaScript, no need to serialize again
            sheet.Data = request.Data ?? "";
            sheet.UpdatedAt = DateTime.UtcNow;

            // Update the existing sheet in database (not create new)
            var success = await _sheetService.UpdateSheetAsync(sheet);
            
            if (success)
            {
                _logger.LogInformation("Sheet {Id} saved successfully. Data length: {DataLength}", id, sheet.Data.Length);
                return Ok(new { success = true, message = "Sheet saved successfully" });
            }
            else
            {
                return StatusCode(500, new { success = false, message = "Error updating sheet" });
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error saving sheet");
            return StatusCode(500, new { success = false, message = "Error saving sheet" });
        }
    }

    [HttpPost("create")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create([FromBody] CreateSheetRequest request)
    {
        try
        {
            if (request == null || string.IsNullOrWhiteSpace(request.FileName))
            {
                return BadRequest(new { success = false, message = "Filename is required" });
            }

            var sanitizedFileName = System.Text.RegularExpressions.Regex.Replace(request.FileName, @"[\\/:*?""<>|]", "").Trim();
            if (string.IsNullOrWhiteSpace(sanitizedFileName))
            {
                return BadRequest(new { success = false, message = "Invalid filename" });
            }

            if (sanitizedFileName.Length > 255)
            {
                return BadRequest(new { success = false, message = "Filename must be 255 characters or fewer" });
            }

            if (!TryGetCurrentUserId(out int userId))
            {
                return Unauthorized();
            }

            var sheet = await _sheetService.SaveSheetAsync(userId, sanitizedFileName, "{\"numsheets\":1,\"currentname\":\"Sheet1\",\"sheetArr\":{\"Sheet1\":{\"name\":\"Sheet1\",\"sheetstr\":{\"savestr\":\"version:1.5\\nsheet:c:1:r:1:tvf:1\\n\"}}},\"currentid\":\"Sheet1\"}");
            if (sheet == null)
            {
                return StatusCode(500, new { success = false, message = "Error creating sheet" });
            }

            return Ok(new { success = true, id = sheet.Id, message = "Sheet created successfully" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating sheet");
            return StatusCode(500, new { success = false, message = "Error creating sheet" });
        }
    }

    [HttpPost("Rename/{id}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Rename(int id, [FromBody] RenameSheetRequest request)
    {
        try
        {
            if (!TryGetCurrentUserId(out int userId)) return Unauthorized();

            if (string.IsNullOrWhiteSpace(request?.FileName))
            {
                return BadRequest(new { success = false, message = "Filename is required" });
            }
            
            var sanitizedFileName = System.Text.RegularExpressions.Regex.Replace(request.FileName, @"[\\/:*?""<>|]", "").Trim();
            if (string.IsNullOrWhiteSpace(sanitizedFileName))
            {
                return BadRequest(new { success = false, message = "Invalid filename" });
            }

            if (sanitizedFileName.Length > 255)
            {
                return BadRequest(new { success = false, message = "Filename must be 255 characters or fewer" });
            }


            var sheet = await _sheetService.GetSheetAsync(id, userId);
            if (sheet == null) return NotFound();

            sheet.FileName = sanitizedFileName;
            sheet.UpdatedAt = DateTime.UtcNow;
            
            var success = await _sheetService.UpdateSheetAsync(sheet);
            
            if (success)
            {
                return Ok(new { success = true, fileName = sheet.FileName });
            }
            
            return StatusCode(500, new { success = false, message = "Failed to rename sheet" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error renaming sheet {SheetId}", id);
            return StatusCode(500, new { success = false, message = "Server error" });
        }
    }

    [HttpPost("delete/{id}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        _logger.LogInformation("Delete request received for sheet ID: {SheetId}", id);
        try
        {
            if (!TryGetCurrentUserId(out int userId))
            {
                _logger.LogWarning("Unauthorized delete attempt for sheet {SheetId}", id);
                return Unauthorized();
            }

            _logger.LogInformation("Attempting to delete sheet {SheetId} for user {UserId}", id, userId);
            var success = await _sheetService.DeleteSheetAsync(id, userId);
            if (!success)
            {
                _logger.LogWarning("Sheet {SheetId} not found or not owned by user {UserId}", id, userId);
                return NotFound();
            }

            _logger.LogInformation("Sheet {SheetId} deleted successfully", id);
            return Ok(new { success = true, message = "Sheet deleted successfully" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting sheet {SheetId}", id);
            return StatusCode(500, new { success = false, message = "Error deleting sheet" });
        }
    }

    [Obsolete("Use /api/spreadsheet/import instead")]
    [HttpPost("import")]
    public IActionResult Import([FromForm] IFormFile file)
    {
        _logger.LogWarning("Legacy import endpoint called. Returning 410 Gone.");
        return StatusCode(410, new { success = false, message = "This endpoint is deprecated. Use /api/spreadsheet/import instead." });
    }

    [HttpGet("download/{id}")]
    public async Task<IActionResult> Download(int id)
    {
        try
        {
            if (!TryGetCurrentUserId(out int userId))
            {
                return Unauthorized();
            }

            var sheet = await _sheetService.GetSheetAsync(id, userId);
            if (sheet == null)
            {
                return NotFound();
            }

            var bytes = await _spreadsheetService.ExportAsync(SpreadsheetData.FromSheet(sheet), "Xlsx");
            if (bytes == null || bytes.Length == 0)
            {
                return StatusCode(500, "Error generating Excel file");
            }

            var fileName = $"{sheet.FileName}.xlsx";
            return File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", fileName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error exporting sheet");
            return StatusCode(500, "Error exporting sheet");
        }
    }

    [HttpGet("export-csv/{id}")]
    public async Task<IActionResult> ExportCsv(int id)
    {
        try
        {
            if (!TryGetCurrentUserId(out int userId))
            {
                return Unauthorized();
            }

            var sheet = await _sheetService.GetSheetAsync(id, userId);
            if (sheet == null)
            {
                return NotFound();
            }

            var bytes = await _spreadsheetService.ExportAsync(SpreadsheetData.FromSheet(sheet), "Csv");
            if (bytes == null || bytes.Length == 0)
            {
                return StatusCode(500, "Error generating CSV file");
            }

            var fileName = $"{sheet.FileName}.csv";
            return File(bytes, "text/csv", fileName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error exporting CSV");
            return StatusCode(500, "Error exporting CSV");
        }
    }

    [HttpGet("export-format/{id}/{format}")]
    public async Task<IActionResult> ExportFormat(int id, string format)
    {
        try
        {
            if (!TryGetCurrentUserId(out int userId))
            {
                return Unauthorized();
            }

            var sheet = await _sheetService.GetSheetAsync(id, userId);
            if (sheet == null)
            {
                return NotFound();
            }

            if (string.IsNullOrWhiteSpace(format))
            {
                return BadRequest("Format is required");
            }
            
            var formatKey = format.ToLower();
            
            var contentTypes = new Dictionary<string, string>
            {
                { "xlsx", "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet" },
                { "xls", "application/vnd.ms-excel" },
                { "csv", "text/csv" },
                { "html", "text/html" },
                { "ods", "application/vnd.oasis.opendocument.spreadsheet" }
            };
            
            if (!contentTypes.ContainsKey(formatKey))
            {
                return BadRequest($"Unsupported format: {System.Net.WebUtility.HtmlEncode(format)}");
            }
            
            var formatCapitalized = char.ToUpper(formatKey[0]) + formatKey.Substring(1);
            
            var bytes = await _spreadsheetService.ExportAsync(SpreadsheetData.FromSheet(sheet), formatCapitalized);
            if (bytes == null || bytes.Length == 0)
            {
                return StatusCode(500, $"Error exporting to {format}");
            }

            var fileName = $"{sheet.FileName}.{formatKey}";
            var contentType = contentTypes.GetValueOrDefault(formatKey, "application/octet-stream");
            
            return File(bytes, contentType, fileName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error exporting {Format}", format);
            return StatusCode(500, "Error generating export file");
        }
    }

    [HttpGet("export-pdf/{id}")]
    public async Task<IActionResult> ExportPdf(int id)
    {
        try
        {
            if (!TryGetCurrentUserId(out int userId))
            {
                return Unauthorized();
            }

            var sheet = await _sheetService.GetSheetAsync(id, userId);
            if (sheet == null)
            {
                return NotFound();
            }

            var bytes = await _spreadsheetService.ExportAsync(SpreadsheetData.FromSheet(sheet), "Pdf");
            if (bytes == null || bytes.Length == 0)
            {
                return StatusCode(500, "Error exporting to PDF");
            }

            var fileName = $"{sheet.FileName}.pdf";
            return File(bytes, "application/pdf", fileName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error exporting PDF");
            return StatusCode(500, "Error exporting PDF");
        }
    }

    [HttpPost("export-gdrive-api/{id}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ExportToGoogleDriveApi(int id, IFormFile file)
    {
        try
        {
            if (!TryGetCurrentUserId(out int userId)) return Unauthorized(new { message = "Unauthorized" });

            var googleScheme = await _schemeProvider.GetSchemeAsync("Google");
            if (googleScheme == null)
            {
                return BadRequest(new { message = "Google integration is not configured. Please set credentials in appsettings." });
            }

            var authResult = await HttpContext.AuthenticateAsync(IdentityConstants.ExternalScheme);
            if (!authResult.Succeeded)
            {
                return Unauthorized(new { message = "Not authenticated with Google", needsAuth = true });
            }

            var accessToken = authResult.Properties?.GetTokenValue("access_token");
            if (string.IsNullOrEmpty(accessToken))
            {
                return Unauthorized(new { message = "Not authenticated with Google", needsAuth = true });
            }

            var sheet = await _sheetService.GetSheetAsync(id, userId);
            if (sheet == null) return NotFound(new { message = "Sheet not found." });

            if (file == null || file.Length == 0)
                return BadRequest(new { message = "No file uploaded." });

            using var excelStream = file.OpenReadStream();

            var credential = GoogleCredential.FromAccessToken(accessToken);
            var driveService = new DriveService(new BaseClientService.Initializer
            {
                HttpClientInitializer = credential,
                ApplicationName = "Social-Calc"
            });

            var fileMetadata = new GoogleFile
            {
                Name = sheet.FileName,
                MimeType = "application/vnd.google-apps.spreadsheet"
            };

            var request = driveService.Files.Create(fileMetadata, excelStream, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet");
            request.Fields = "id, webViewLink";
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(HttpContext.RequestAborted);
            cts.CancelAfter(TimeSpan.FromSeconds(60));

            var response = await request.UploadAsync(cts.Token);

            if (response.Status == Google.Apis.Upload.UploadStatus.Failed)
            {
                _logger.LogError(response.Exception, "Google Drive upload failed: {Message}", response.Exception?.Message);
                return StatusCode(500, new { message = "Failed to upload to Google Drive." });
            }

            var uploadedFile = request.ResponseBody;
            
            if (string.IsNullOrEmpty(uploadedFile?.WebViewLink))
            {
                _logger.LogError("Google Drive upload succeeded but returned no web view link.");
                return StatusCode(500, new { message = "Failed to retrieve Google Drive link." });
            }
            
            return Ok(new { success = true, link = uploadedFile.WebViewLink });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error exporting to Google Drive");
            return StatusCode(500, new { message = "Error exporting to Google Drive" });
        }
    }

    [HttpGet("export-gdrive/{id}")]
    public async Task<IActionResult> ExportToGoogleDrive(int id)
    {
        try
        {
            if (!TryGetCurrentUserId(out int userId)) return Unauthorized();

            var googleScheme = await _schemeProvider.GetSchemeAsync("Google");
            if (googleScheme == null)
            {
                return BadRequest("Google integration is not configured. Please set credentials in appsettings.");
            }

            var authResult = await HttpContext.AuthenticateAsync(IdentityConstants.ExternalScheme);
            if (!authResult.Succeeded)
            {
                var properties = new AuthenticationProperties { RedirectUri = Url.Action("ExportToGoogleDrive", new { id }) };
                return Challenge(properties, "Google");
            }

            var accessToken = authResult.Properties?.GetTokenValue("access_token");
            if (string.IsNullOrEmpty(accessToken))
            {
                var properties = new AuthenticationProperties { RedirectUri = Url.Action("ExportToGoogleDrive", new { id }) };
                return Challenge(properties, "Google");
            }

            var sheet = await _sheetService.GetSheetAsync(id, userId);
            if (sheet == null) return NotFound();

            var bytes = await _spreadsheetService.ExportAsync(SpreadsheetData.FromSheet(sheet), "Xlsx");
            if (bytes == null || bytes.Length == 0) return StatusCode(500, "Error generating Excel file for export.");
            using var excelStream = new MemoryStream(bytes);

            var credential = GoogleCredential.FromAccessToken(accessToken);
            var driveService = new DriveService(new BaseClientService.Initializer
            {
                HttpClientInitializer = credential,
                ApplicationName = "Social-Calc"
            });

            var fileMetadata = new GoogleFile
            {
                Name = sheet.FileName,
                MimeType = "application/vnd.google-apps.spreadsheet"
            };

            var request = driveService.Files.Create(fileMetadata, excelStream, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet");
            request.Fields = "id, webViewLink";
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(HttpContext.RequestAborted);
            cts.CancelAfter(TimeSpan.FromSeconds(60));

            var response = await request.UploadAsync(cts.Token);

            if (response.Status == Google.Apis.Upload.UploadStatus.Failed)
            {
                _logger.LogError(response.Exception, "Google Drive upload failed: {Message}", response.Exception?.Message);
                return StatusCode(500, "Failed to upload to Google Drive.");
            }

            var uploadedFile = request.ResponseBody;
            
            if (string.IsNullOrEmpty(uploadedFile?.WebViewLink))
            {
                _logger.LogError("Google Drive upload succeeded but returned no web view link.");
                return StatusCode(500, "Failed to retrieve Google Drive link.");
            }
            
            return Redirect(uploadedFile.WebViewLink);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error exporting to Google Drive");
            return StatusCode(500, "Error exporting to Google Drive");
        }
    }
}


