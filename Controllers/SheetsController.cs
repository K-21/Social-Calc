using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using SocialCalc.Web.Models;
using SocialCalc.Web.Services;
using System.Text.Json;

namespace SocialCalc.Web.Controllers;

[Authorize]
[Route("/sheets")]
public class SheetsController : Controller
{
    private readonly ISheetService _sheetService;
    private readonly IExcelService _excelService;
    private readonly UserManager<User> _userManager;
    private readonly ILogger<SheetsController> _logger;

    public SheetsController(
        ISheetService sheetService,
        IExcelService excelService,
        UserManager<User> userManager,
        ILogger<SheetsController> logger)
    {
        _sheetService = sheetService;
        _excelService = excelService;
        _userManager = userManager;
        _logger = logger;
    }

    [HttpGet("")]
    public async Task<IActionResult> Index()
    {
        try
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return RedirectToAction("Login", "Auth");
            }

            var sheets = await _sheetService.GetUserSheetsAsync(user.Id);
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
    public async Task<IActionResult> Editor(int id)
    {
        try
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return RedirectToAction("Login", "Auth");
            }

            var sheet = await _sheetService.GetSheetAsync(id, user.Id);
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
    public async Task<IActionResult> Save(int id, [FromBody] SaveSheetRequest request)
    {
        try
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return Unauthorized();
            }

            var sheet = await _sheetService.GetSheetAsync(id, user.Id);
            if (sheet == null)
            {
                return NotFound();
            }

            // Log what we received
            _logger.LogInformation($"Save request received. Data length: {request.Data?.Length ?? 0}");
            _logger.LogInformation($"Data preview (first 500 chars): {request.Data?.Substring(0, Math.Min(500, request.Data?.Length ?? 0))}");

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
    public async Task<IActionResult> Create([FromBody] CreateSheetRequest request)
    {
        try
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return Unauthorized();
            }

            var sheet = await _sheetService.SaveSheetAsync(user.Id, request.FileName, "{}");
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

    [HttpPost("delete/{id}")]
    public async Task<IActionResult> Delete(int id)
    {
        _logger.LogInformation($"Delete request received for sheet ID: {id}");
        try
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                _logger.LogWarning($"Unauthorized delete attempt for sheet {id}");
                return Unauthorized();
            }

            _logger.LogInformation($"Attempting to delete sheet {id} for user {user.Id}");
            var success = await _sheetService.DeleteSheetAsync(id, user.Id);
            if (!success)
            {
                _logger.LogWarning($"Sheet {id} not found or not owned by user {user.Id}");
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

    [HttpPost("import")]
    public async Task<IActionResult> Import([FromForm] IFormFile file)
    {
        try
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
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
                var sheet = await _excelService.ImportFromExcelAsync(stream, user.Id, fileName);
                if (sheet == null)
                {
                    return StatusCode(500, new { success = false, message = "Error importing file" });
                }

                // Save the imported sheet to database
                var savedSheet = await _sheetService.SaveSheetAsync(user.Id, sheet.FileName, sheet.Data);
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
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return Unauthorized();
            }

            var sheet = await _sheetService.GetSheetAsync(id, user.Id);
            if (sheet == null)
            {
                return NotFound();
            }

            var excelStream = await _excelService.ExportToExcelAsync(sheet);
            if (excelStream == null)
            {
                return StatusCode(500, "Error exporting sheet");
            }

            var fileName = $"{sheet.FileName}_{DateTime.Now:yyyyMMddHHmmss}.xlsx";
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
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return Unauthorized();
            }

            var sheet = await _sheetService.GetSheetAsync(id, user.Id);
            if (sheet == null)
            {
                return NotFound();
            }

            var csvStream = await _excelService.ExportToCsvAsync(sheet);
            if (csvStream == null)
            {
                return StatusCode(500, "Error exporting to CSV");
            }

            var fileName = $"{sheet.FileName}_{DateTime.Now:yyyyMMddHHmmss}.csv";
            return File(csvStream, "text/csv", fileName);
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error exporting CSV: {ex.Message}");
            return StatusCode(500, "Error exporting CSV");
        }
    }

    [HttpGet("export-format/{id}/{format}")]
    public async Task<IActionResult> ExportFormat(int id, string format)
    {
        try
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return Unauthorized();
            }

            var sheet = await _sheetService.GetSheetAsync(id, user.Id);
            if (sheet == null)
            {
                return NotFound();
            }

            var contentTypes = new Dictionary<string, string>
            {
                { "xlsx", "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet" },
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

            var fileName = $"{sheet.FileName}_{DateTime.Now:yyyyMMddHHmmss}.{formatKey}";
            var contentType = contentTypes.GetValueOrDefault(formatKey, "application/octet-stream");
            
            return File(stream, contentType, fileName);
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error exporting {format}: {ex.Message}");
            return StatusCode(500, $"Error exporting {format}");
        }
    }

    // Dev-only endpoint: export without authentication (for local testing)
    [AllowAnonymous]
    [HttpGet("export-noauth/{id}")]
    public async Task<IActionResult> ExportNoAuth(int id)
    {
        try
        {
            var sheet = await _sheetService.GetSheetByIdAsync(id);
            if (sheet == null)
            {
                return NotFound();
            }

            var excelStream = await _excelService.ExportToExcelAsync(sheet);
            if (excelStream == null)
            {
                return StatusCode(500, "Error exporting sheet");
            }

            var fileName = $"{sheet.FileName}_{DateTime.Now:yyyyMMddHHmmss}.xlsx";
            return File(excelStream, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", fileName);
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error exporting sheet (noauth): {ex.Message}");
            return StatusCode(500, "Error exporting sheet");
        }
    }

    [HttpGet("export-pdf/{id}")]
    public async Task<IActionResult> ExportPdf(int id)
    {
        try
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return Unauthorized();
            }

            var sheet = await _sheetService.GetSheetAsync(id, user.Id);
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

            var fileName = $"{sheet.FileName}_{DateTime.Now:yyyyMMddHHmmss}.pdf";
            var fileBytes = System.IO.File.ReadAllBytes(pdfPath);
            return File(fileBytes, "application/pdf", fileName);
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error exporting PDF: {ex.Message}");
            return StatusCode(500, "Error exporting PDF");
        }
    }
}

// Request DTOs
public class SaveSheetRequest
{
    public string Data { get; set; } = "";
}

public class CreateSheetRequest
{
    public string FileName { get; set; } = "";
}
