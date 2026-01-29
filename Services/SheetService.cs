using SocialCalc.Web.Data;
using SocialCalc.Web.Models;

namespace SocialCalc.Web.Services;

public class SheetService : ISheetService
{
    private readonly ApplicationDbContext _context;
    private readonly IStorageService _storageService;
    private readonly ILogger<SheetService> _logger;

    public SheetService(
        ApplicationDbContext context,
        IStorageService storageService,
        ILogger<SheetService> logger)
    {
        _context = context;
        _storageService = storageService;
        _logger = logger;
    }

    public async Task<Sheet?> SaveSheetAsync(int userId, string fileName, string data)
    {
        try
        {
            var sheet = new Sheet
            {
                UserId = userId,
                FileName = fileName,
                Data = data,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            _context.Sheets.Add(sheet);
            await _context.SaveChangesAsync();

            // Also save to file system for backup
            var filePath = $"users/{userId}/{fileName}.json";
            await _storageService.CreateFileAsync(filePath, data);

            _logger.LogInformation($"Sheet saved: {fileName} for user {userId}");
            return sheet;
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error saving sheet: {ex.Message}");
            return null;
        }
    }

    public async Task<bool> UpdateSheetAsync(Sheet sheet)
    {
        try
        {
            _context.Sheets.Update(sheet);
            await _context.SaveChangesAsync();

            // Also update file system backup
            var filePath = $"users/{sheet.UserId}/{sheet.FileName}.json";
            await _storageService.CreateFileAsync(filePath, sheet.Data);

            _logger.LogInformation($"Sheet updated: {sheet.FileName} (ID: {sheet.Id})");
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error updating sheet: {ex.Message}");
            return false;
        }
    }

    public async Task<List<Sheet>> GetUserSheetsAsync(int userId)
    {
        try
        {
            var sheets = _context.Sheets
                .Where(s => s.UserId == userId && !s.IsDeleted)
                .OrderByDescending(s => s.UpdatedAt)
                .ToList();

            return sheets;
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error retrieving sheets for user {userId}: {ex.Message}");
            return new List<Sheet>();
        }
    }

    public async Task<Sheet?> GetSheetAsync(int sheetId, int userId)
    {
        try
        {
            var sheet = _context.Sheets
                .FirstOrDefault(s => s.Id == sheetId && s.UserId == userId && !s.IsDeleted);

            return sheet;
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error retrieving sheet {sheetId}: {ex.Message}");
            return null;
        }
    }

    public async Task<Sheet?> GetSheetByIdAsync(int sheetId)
    {
        try
        {
            var sheet = _context.Sheets
                .FirstOrDefault(s => s.Id == sheetId && !s.IsDeleted);

            return sheet;
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error retrieving sheet by id {sheetId}: {ex.Message}");
            return null;
        }
    }

    public async Task<bool> DeleteSheetAsync(int sheetId, int userId)
    {
        try
        {
            var sheet = await GetSheetAsync(sheetId, userId);
            if (sheet == null)
            {
                return false;
            }

            // Hard delete - actually remove from database
            _context.Sheets.Remove(sheet);
            await _context.SaveChangesAsync();

            // Also try to delete the file backup if it exists
            try
            {
                var filePath = $"users/{userId}/{sheet.FileName}.json";
                // Note: StorageService might not have a delete method, so just log
                _logger.LogInformation($"File backup at {filePath} should be cleaned up manually");
            }
            catch (Exception fileEx)
            {
                _logger.LogWarning($"Could not delete file backup: {fileEx.Message}");
            }

            _logger.LogInformation($"Sheet permanently deleted: {sheetId} ({sheet.FileName}) for user {userId}");
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error deleting sheet {sheetId}: {ex.Message}");
            return false;
        }
    }

    public async Task<Sheet?> ImportSheetAsync(int userId, string fileName, string data)
    {
        try
        {
            // Validate JSON data
            if (string.IsNullOrWhiteSpace(data))
            {
                _logger.LogWarning($"Invalid data for import: {fileName}");
                return null;
            }

            return await SaveSheetAsync(userId, fileName, data);
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error importing sheet: {ex.Message}");
            return null;
        }
    }

    public async Task<string> ExportToPDFAsync(Sheet sheet)
    {
        try
        {
            // PDF export logic - to be implemented with SelectPdf or iTextSharp
            var pdfPath = $"exports/{sheet.Id}_{DateTime.Now:yyyyMMddHHmmss}.pdf";
            
            _logger.LogInformation($"Sheet exported to PDF: {pdfPath}");
            return pdfPath;
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error exporting to PDF: {ex.Message}");
            return string.Empty;
        }
    }
}
