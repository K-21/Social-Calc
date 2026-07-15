using SocialCalc.Web.Models;

namespace SocialCalc.Web.Services;

/// <summary>
/// Authentication service interface - replaces cloud/authenticate/
/// </summary>
public interface IAuthService
{
    Task<User?> AuthenticateUserAsync(string email, string password);
    Task<bool> RegisterUserAsync(string email, string password);
    Task<bool> ValidateUserAsync(User user);
    Task<string> GeneratePasswordResetTokenAsync(User user);
    Task<bool> ResetPasswordAsync(string token, string newPassword);
    Task UpdateLastLoginAsync(User user);
}


/// <summary>
/// Sheet service interface - handles sheet operations
/// </summary>
public interface ISheetService
{
    Task<Sheet?> SaveSheetAsync(int userId, string fileName, string data);
    Task<bool> UpdateSheetAsync(Sheet sheet);
    Task<List<Sheet>> GetUserSheetsAsync(int userId, int page = 1, int pageSize = 50);
    Task<int> GetTotalUserSheetsAsync(int userId);
    Task<Sheet?> GetSheetAsync(int sheetId, int userId);
    Task<Sheet?> GetSheetByIdAsync(int sheetId, int userId);
    Task<bool> DeleteSheetAsync(int sheetId, int userId);
}

/// <summary>
/// Excel service interface - handles PHP Excel integration
/// </summary>
public interface IExcelService
{
    Task<Stream?> ExportToExcelAsync(Sheet sheet);
    Task<Stream?> ExportToCsvAsync(Sheet sheet);
    Task<Stream?> ExportToFormatAsync(Sheet sheet, string format); // format: "Csv", "Html", "Ods", "Xlsx"
    Task<Sheet?> ImportFromExcelAsync(Stream fileStream, int userId, string fileName);
    Task<bool> IsValidExcelFileAsync(Stream fileStream);
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

