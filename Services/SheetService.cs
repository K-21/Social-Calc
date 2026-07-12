using SocialCalc.Web.Data;
using SocialCalc.Web.Models;
using Microsoft.EntityFrameworkCore;

namespace SocialCalc.Web.Services;

public class SheetService : ISheetService
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<SheetService> _logger;

    public SheetService(
        ApplicationDbContext context,
        ILogger<SheetService> logger)
    {
        _context = context;
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

            _logger.LogInformation($"Sheet updated: {sheet.FileName} (ID: {sheet.Id})");
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error updating sheet: {ex.Message}");
            return false;
        }
    }

    public async Task<List<Sheet>> GetUserSheetsAsync(int userId, int page = 1, int pageSize = 50)
    {
        try
        {
            var sheets = await _context.Sheets
                .Where(s => s.UserId == userId && !s.IsDeleted)
                .OrderByDescending(s => s.UpdatedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            return sheets;
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error retrieving sheets for user {userId}: {ex.Message}");
            return new List<Sheet>();
        }
    }

    public async Task<int> GetTotalUserSheetsAsync(int userId)
    {
        try
        {
            return await _context.Sheets.CountAsync(s => s.UserId == userId && !s.IsDeleted);
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error counting sheets for user {userId}: {ex.Message}");
            return 0;
        }
    }

    public async Task<Sheet?> GetSheetAsync(int sheetId, int userId)
    {
        try
        {
            var sheet = await _context.Sheets
                .FirstOrDefaultAsync(s => s.Id == sheetId && s.UserId == userId && !s.IsDeleted);

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
            var sheet = await _context.Sheets
                .FirstOrDefaultAsync(s => s.Id == sheetId && !s.IsDeleted);

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

    public Task<string> ExportToPDFAsync(Sheet sheet)
    {
        return Task.FromResult("Error: PDF Export is not yet available.");
    }
}
