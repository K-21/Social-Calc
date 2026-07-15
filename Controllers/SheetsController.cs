using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using SocialCalc.Web.Models;
using SocialCalc.Web.Services;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication;
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
    private readonly IExcelService _excelService;
    private readonly UserManager<User> _userManager;
    private readonly ILogger<SheetsController> _logger;
    private readonly IConfiguration _configuration;

    public SheetsController(
        ISheetService sheetService,
        IExcelService excelService,
        UserManager<User> userManager,
        ILogger<SheetsController> logger,
        IConfiguration configuration)
    {
        _sheetService = sheetService;
        _excelService = excelService;
        _userManager = userManager;
        _logger = logger;
        _configuration = configuration;
    }

    [HttpGet("")]
    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public async Task<IActionResult> Index(int page = 1)
    {
        try
        {
            var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userIdStr) || !int.TryParse(userIdStr, out int userId))
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
            _logger.LogError($"Error retrieving sheets: {ex.Message}");
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
            var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userIdStr) || !int.TryParse(userIdStr, out int userId))
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
            _logger.LogError($"Error loading sheet editor: {ex.Message}");
            return NotFound();
        }
    }

    [HttpPost("save/{id}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Save(int id, [FromBody] SaveSheetRequest request)
    {
        try
        {
            var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userIdStr) || !int.TryParse(userIdStr, out int userId))
            {
                return Unauthorized();
            }

            var sheet = await _sheetService.GetSheetAsync(id, userId);
            if (sheet == null)
            {
                return NotFound();
            }

            // Log what we received
            _logger.LogInformation($"Save request received. Data length: {request.Data?.Length ?? 0}");

            // Data is already a string from JavaScript, no need to serialize again
            sheet.Data = request.Data ?? "";
            sheet.UpdatedAt = DateTime.UtcNow;

            // Update the existing sheet in database (not create new)
            var success = await _sheetService.UpdateSheetAsync(sheet);
            
            if (success)
            {
                _logger.LogInformation($"Sheet {id} saved successfully. Data length: {sheet.Data.Length}");
                return Ok(new { success = true, message = "Sheet saved successfully" });
            }
            else
            {
                return StatusCode(500, new { success = false, message = "Error updating sheet" });
            }
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error saving sheet: {ex.Message}");
            return StatusCode(500, new { success = false, message = "Error saving sheet" });
        }
    }

    [HttpPost("create")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create([FromBody] CreateSheetRequest request)
    {
        try
        {
            var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userIdStr) || !int.TryParse(userIdStr, out int userId))
            {
                return Unauthorized();
            }

            var sheet = await _sheetService.SaveSheetAsync(userId, request.FileName, "{\"numsheets\":1,\"currentname\":\"Sheet1\",\"sheetArr\":{\"Sheet1\":{\"name\":\"Sheet1\",\"sheetstr\":{\"savestr\":\"version:1.5\\nsheet:c:1:r:1:tvf:1\\n\"}}},\"currentid\":\"Sheet1\"}");
            if (sheet == null)
            {
                return StatusCode(500, new { success = false, message = "Error creating sheet" });
            }

            return Ok(new { success = true, id = sheet.Id, message = "Sheet created successfully" });
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error creating sheet: {ex.Message}");
            return StatusCode(500, new { success = false, message = "Error creating sheet" });
        }
    }

    [HttpPost("Rename/{id}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Rename(int id, [FromBody] RenameSheetRequest request)
    {
        try
        {
            var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userIdStr) || !int.TryParse(userIdStr, out int userId)) return Unauthorized();

            if (string.IsNullOrWhiteSpace(request?.FileName))
            {
                return BadRequest(new { success = false, message = "Filename is required" });
            }
            
            var sanitizedFileName = System.Text.RegularExpressions.Regex.Replace(request.FileName, @"[^a-zA-Z0-9 _\-\.]", "");
            if (string.IsNullOrWhiteSpace(sanitizedFileName))
            {
                return BadRequest(new { success = false, message = "Invalid filename" });
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
            _logger.LogError($"Error renaming sheet {id}: {ex.Message}");
            return StatusCode(500, new { success = false, message = "Server error" });
        }
    }

    [HttpPost("delete/{id}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        _logger.LogInformation($"Delete request received for sheet ID: {id}");
        try
        {
            var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userIdStr) || !int.TryParse(userIdStr, out int userId))
            {
                _logger.LogWarning($"Unauthorized delete attempt for sheet {id}");
                return Unauthorized();
            }

            _logger.LogInformation($"Attempting to delete sheet {id} for user {userId}");
            var success = await _sheetService.DeleteSheetAsync(id, userId);
            if (!success)
            {
                _logger.LogWarning($"Sheet {id} not found or not owned by user {userId}");
                return NotFound();
            }

            _logger.LogInformation($"Sheet {id} deleted successfully");
            return Ok(new { success = true, message = "Sheet deleted successfully" });
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error deleting sheet {id}: {ex.Message}");
            return StatusCode(500, new { success = false, message = "Error deleting sheet" });
        }
    }

    // LEGACY: Use /api/spreadsheet/import instead
    [HttpPost("import")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Import([FromForm] IFormFile file)
    {
        try
        {
            var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userIdStr) || !int.TryParse(userIdStr, out int userId))
            {
                return Unauthorized();
            }

            if (file == null || file.Length == 0)
            {
                return BadRequest("No file uploaded");
            }

            var fileName = Path.GetFileNameWithoutExtension(file.FileName);
            using (var stream = file.OpenReadStream())
            {
                var sheet = await _excelService.ImportFromExcelAsync(stream, userId, fileName);
                if (sheet == null)
                {
                    return StatusCode(500, new { success = false, message = "Error importing file" });
                }

                // Save the imported sheet to database
                var savedSheet = await _sheetService.SaveSheetAsync(userId, sheet.FileName, sheet.Data);
                if (savedSheet == null)
                {
                    return StatusCode(500, new { success = false, message = "Error saving imported sheet" });
                }

                _logger.LogInformation($"Sheet imported and saved: {savedSheet.FileName} (ID: {savedSheet.Id})");
                return Ok(new { success = true, id = savedSheet.Id, message = "Sheet imported successfully" });
            }
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error importing sheet: {ex.Message}");
            return StatusCode(500, new { success = false, message = "Error importing sheet" });
        }
    }

    [HttpGet("export/{id}")]
    public async Task<IActionResult> Export(int id)
    {
        try
        {
            var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userIdStr) || !int.TryParse(userIdStr, out int userId))
            {
                return Unauthorized();
            }

            var sheet = await _sheetService.GetSheetAsync(id, userId);
            if (sheet == null)
            {
                return NotFound();
            }

            var excelStream = await _excelService.ExportToExcelAsync(sheet);
            if (excelStream == null)
            {
                return StatusCode(500, "Error exporting sheet");
            }

            var fileName = $"{sheet.FileName}.xlsx";
            return File(excelStream, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", fileName);
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error exporting sheet: {ex.Message}");
            return StatusCode(500, "Error exporting sheet");
        }
    }

    [HttpGet("export-csv/{id}")]
    public async Task<IActionResult> ExportCsv(int id)
    {
        try
        {
            var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userIdStr) || !int.TryParse(userIdStr, out int userId))
            {
                return Unauthorized();
            }

            var sheet = await _sheetService.GetSheetAsync(id, userId);
            if (sheet == null)
            {
                return NotFound();
            }

            var csvStream = await _excelService.ExportToCsvAsync(sheet);
            if (csvStream == null)
            {
                return StatusCode(500, "Error exporting to CSV");
            }

            var fileName = $"{sheet.FileName}.csv";
            return File(csvStream, "text/csv", fileName);
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error exporting CSV: {ex.Message}");
            return StatusCode(500, "Error exporting CSV");
        }
    }

    // LEGACY: Use JS export or /api/spreadsheet/export/{id} instead
    [HttpGet("export-format/{id}/{format}")]
    public async Task<IActionResult> ExportFormat(int id, string format)
    {
        try
        {
            var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userIdStr) || !int.TryParse(userIdStr, out int userId))
            {
                return Unauthorized();
            }

            var sheet = await _sheetService.GetSheetAsync(id, userId);
            if (sheet == null)
            {
                return NotFound();
            }

            var contentTypes = new Dictionary<string, string>
            {
                { "xlsx", "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet" },
                { "xls", "application/vnd.ms-excel" },
                { "csv", "text/csv" },
                { "html", "text/html" },
                { "ods", "application/vnd.oasis.opendocument.spreadsheet" }
            };

            var formatKey = format.ToLower();
            var formatCapitalized = char.ToUpper(format[0]) + format.Substring(1).ToLower();
            
            var stream = await _excelService.ExportToFormatAsync(sheet, formatCapitalized);
            if (stream == null)
            {
                return StatusCode(500, $"Error exporting to {format}");
            }

            var fileName = $"{sheet.FileName}.{formatKey}";
            var contentType = contentTypes.GetValueOrDefault(formatKey, "application/octet-stream");
            
            return File(stream, contentType, fileName);
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error exporting {format}: {ex.Message}");
            return StatusCode(500, $"Error exporting {format}");
        }
    }


    [HttpGet("export-pdf/{id}")]
    public async Task<IActionResult> ExportPdf(int id)
    {
        try
        {
            var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userIdStr) || !int.TryParse(userIdStr, out int userId))
            {
                return Unauthorized();
            }

            var sheet = await _sheetService.GetSheetAsync(id, userId);
            if (sheet == null)
            {
                return NotFound();
            }

            // Use the PHP CLI export service for PDF
            var pdfStream = await _excelService.ExportToFormatAsync(sheet, "Pdf");
            if (pdfStream == null)
            {
                return StatusCode(500, "Error exporting to PDF");
            }

            var fileName = $"{sheet.FileName}.pdf";
            return File(pdfStream, "application/pdf", fileName);
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error exporting PDF: {ex.Message}");
            return StatusCode(500, "Error exporting PDF");
        }
    }

    [HttpGet("export-gdrive/{id}")]
    public async Task<IActionResult> ExportToGoogleDrive(int id)
    {
        try
        {
            var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userIdStr) || !int.TryParse(userIdStr, out int userId)) return Unauthorized();

            // 1. Check if user is authenticated with Google and has a token
            var authResult = await HttpContext.AuthenticateAsync(IdentityConstants.ExternalScheme);
            if (!authResult.Succeeded)
            {
                // Challenge Google authentication, redirect back here when done
                var properties = new AuthenticationProperties { RedirectUri = Url.Action("ExportToGoogleDrive", new { id }) };
                return Challenge(properties, "Google");
            }

            var accessToken = authResult.Properties?.GetTokenValue("access_token");
            if (string.IsNullOrEmpty(accessToken))
            {
                // Force re-authentication if token is missing
                var properties = new AuthenticationProperties { RedirectUri = Url.Action("ExportToGoogleDrive", new { id }) };
                return Challenge(properties, "Google");
            }

            // 2. Get the sheet data as Excel stream
            var sheet = await _sheetService.GetSheetAsync(id, userId);
            if (sheet == null) return NotFound();

            var excelStream = await _excelService.ExportToExcelAsync(sheet);
            if (excelStream == null) return StatusCode(500, "Error generating Excel file for export.");

            // 3. Connect to Google Drive API
            var credential = GoogleCredential.FromAccessToken(accessToken);
            var driveService = new DriveService(new BaseClientService.Initializer
            {
                HttpClientInitializer = credential,
                ApplicationName = "Social-Calc"
            });

            // 4. Upload and convert to native Google Sheet
            var fileMetadata = new GoogleFile
            {
                Name = sheet.FileName,
                MimeType = "application/vnd.google-apps.spreadsheet" // Auto-convert to Google Sheet
            };

            var request = driveService.Files.Create(fileMetadata, excelStream, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet");
            request.Fields = "id, webViewLink";
            var response = await request.UploadAsync();

            if (response.Status == Google.Apis.Upload.UploadStatus.Failed)
            {
                _logger.LogError($"Google Drive upload failed: {response.Exception?.Message}");
                return StatusCode(500, "Failed to upload to Google Drive.");
            }

            var uploadedFile = request.ResponseBody;
            
            // Redirect to the newly created Google Sheet
            return Redirect(uploadedFile.WebViewLink);
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error exporting to Google Drive: {ex.Message}");
            return StatusCode(500, "Error exporting to Google Drive");
        }
    }
}

// Request DTOs
public class SaveSheetRequest
{
    public string Data { get; set; } = null!;
}

public class RenameSheetRequest
{
    public string FileName { get; set; } = null!;
}

public class CreateSheetRequest
{
    public string FileName { get; set; } = "";
}
