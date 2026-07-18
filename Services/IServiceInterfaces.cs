using SocialCalc.Web.Models;

namespace SocialCalc.Web.Services;

/// <summary>
/// Authentication service interface - replaces cloud/authenticate/
/// </summary>
public interface IAuthService
{
    Task<User?> AuthenticateUserAsync(string email, string password);
    Task<(User? User, IEnumerable<string> Errors)> RegisterUserAsync(string email, string password);
    Task<bool> ValidateUserAsync(User user);
    Task<string> GeneratePasswordResetTokenAsync(User user);
    Task<(bool Success, IEnumerable<string> Errors)> ResetPasswordAsync(string email, string token, string newPassword);
    Task UpdateLastLoginAsync(User user);
    Task<User?> FindUserByEmailAsync(string email);
}


/// <summary>
/// Sheet service interface - handles sheet operations
/// </summary>
public interface ISheetService
{
    Task<Sheet?> SaveSheetAsync(int userId, string fileName, string data);
    Task<bool> UpdateSheetAsync(Sheet sheet);
    Task<List<Sheet>> GetUserSheetsAsync(int userId, int page = 1, int pageSize = 10);
    Task<int> GetTotalUserSheetsAsync(int userId);
    Task<Sheet?> GetSheetAsync(int sheetId, int userId);

    Task<bool> DeleteSheetAsync(int sheetId, int userId);
}

/// <summary>
/// Spreadsheet service interface - handles parsing and exporting
/// </summary>
public interface ISpreadsheetService
{
    Task<bool> IsValidExcelFileAsync(Stream fileStream, string fileName);
    Task<SpreadsheetData> ImportAsync(Stream file, string format);
    Task<byte[]> ExportAsync(SpreadsheetData data, string format);
}

/// <summary>
/// Email service interface - handles email notifications
/// </summary>
public interface IEmailService
{
    Task<bool> SendPasswordResetEmailAsync(string email, string resetLink);
    Task<bool> SendWelcomeEmailAsync(string email, string userName);
    Task<bool> SendSheetSharedNotificationAsync(string email, string sheetName);
}

