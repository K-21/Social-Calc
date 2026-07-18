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


            _logger.LogInformation("Sheet saved: {FileName} for user {UserId}", fileName, userId);
            return sheet;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error saving sheet: {FileName}", fileName);
            return null;
        }
    }

    public async Task<bool> UpdateSheetAsync(Sheet sheet)
    {
        try
        {
            sheet.UpdatedAt = DateTime.UtcNow;
            _context.Sheets.Update(sheet);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Sheet updated: {FileName} (ID: {SheetId})", sheet.FileName, sheet.Id);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating sheet: {FileName}", sheet?.FileName);
            return false;
        }
    }

    public async Task<List<Sheet>> GetUserSheetsAsync(int userId, int page = 1, int pageSize = 10)
    {
        try
        {
            var sheets = await _context.Sheets
                .Where(s => s.UserId == userId)
                .OrderByDescending(s => s.UpdatedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            return sheets;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving sheets for user {UserId}", userId);
            return new List<Sheet>();
        }
    }

    public async Task<int> GetTotalUserSheetsAsync(int userId)
    {
        try
        {
            return await _context.Sheets.CountAsync(s => s.UserId == userId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error counting sheets for user {UserId}", userId);
            return 0;
        }
    }

    public async Task<Sheet?> GetSheetAsync(int sheetId, int userId)
    {
        try
        {
            var sheet = await _context.Sheets
                .FirstOrDefaultAsync(s => s.Id == sheetId && s.UserId == userId);

            return sheet;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving sheet {SheetId}", sheetId);
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

            // Soft delete
            sheet.IsDeleted = true;
            sheet.UpdatedAt = DateTime.UtcNow;
            _context.Sheets.Update(sheet);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Sheet soft deleted: {SheetId} ({FileName}) for user {UserId}", sheetId, sheet.FileName, userId);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting sheet {SheetId}", sheetId);
            return false;
        }
    }
}
