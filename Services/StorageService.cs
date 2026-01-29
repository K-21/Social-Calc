using SocialCalc.Web.Models;

namespace SocialCalc.Web.Services;

public class StorageService : IStorageService
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<StorageService> _logger;
    private readonly string _basePath;

    public StorageService(
        IConfiguration configuration,
        ILogger<StorageService> logger)
    {
        _configuration = configuration;
        _logger = logger;
        _basePath = Path.Combine(
            Directory.GetCurrentDirectory(),
            _configuration["FileStorage:BasePath"] ?? "uploads/"
        );

        // Ensure base directory exists
        Directory.CreateDirectory(_basePath);
    }

    public async Task<string?> GetFileAsync(string path)
    {
        try
        {
            var fullPath = Path.Combine(_basePath, path);

            // Security: Prevent directory traversal
            var resolvedPath = Path.GetFullPath(fullPath);
            if (!resolvedPath.StartsWith(_basePath))
            {
                _logger.LogWarning($"Attempted unauthorized file access: {path}");
                return null;
            }

            if (!File.Exists(fullPath))
            {
                _logger.LogWarning($"File not found: {path}");
                return null;
            }

            var content = await File.ReadAllTextAsync(fullPath);
            return content;
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error reading file {path}: {ex.Message}");
            return null;
        }
    }

    public async Task<bool> CreateFileAsync(string path, string data)
    {
        try
        {
            var fullPath = Path.Combine(_basePath, path);
            var resolvedPath = Path.GetFullPath(fullPath);

            if (!resolvedPath.StartsWith(_basePath))
            {
                _logger.LogWarning($"Attempted unauthorized file creation: {path}");
                return false;
            }

            var directory = Path.GetDirectoryName(fullPath);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            await File.WriteAllTextAsync(fullPath, data);
            _logger.LogInformation($"File created: {path}");
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error creating file {path}: {ex.Message}");
            return false;
        }
    }

    public async Task<bool> UpdateFileAsync(string path, string data)
    {
        try
        {
            var fullPath = Path.Combine(_basePath, path);
            var resolvedPath = Path.GetFullPath(fullPath);

            if (!resolvedPath.StartsWith(_basePath))
            {
                _logger.LogWarning($"Attempted unauthorized file update: {path}");
                return false;
            }

            if (!File.Exists(fullPath))
            {
                _logger.LogWarning($"File not found for update: {path}");
                return false;
            }

            await File.WriteAllTextAsync(fullPath, data);
            _logger.LogInformation($"File updated: {path}");
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error updating file {path}: {ex.Message}");
            return false;
        }
    }

    public async Task<bool> CreateDirectoryAsync(string path)
    {
        try
        {
            var fullPath = Path.Combine(_basePath, path);
            var resolvedPath = Path.GetFullPath(fullPath);

            if (!resolvedPath.StartsWith(_basePath))
            {
                _logger.LogWarning($"Attempted unauthorized directory creation: {path}");
                return false;
            }

            Directory.CreateDirectory(fullPath);
            _logger.LogInformation($"Directory created: {path}");
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error creating directory {path}: {ex.Message}");
            return false;
        }
    }

    public async Task<bool> DeleteFileAsync(string path)
    {
        try
        {
            var fullPath = Path.Combine(_basePath, path);
            var resolvedPath = Path.GetFullPath(fullPath);

            if (!resolvedPath.StartsWith(_basePath))
            {
                _logger.LogWarning($"Attempted unauthorized file deletion: {path}");
                return false;
            }

            if (!File.Exists(fullPath))
            {
                _logger.LogWarning($"File not found for deletion: {path}");
                return false;
            }

            File.Delete(fullPath);
            _logger.LogInformation($"File deleted: {path}");
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error deleting file {path}: {ex.Message}");
            return false;
        }
    }

    public async Task<List<string>> ListFilesAsync(string path)
    {
        try
        {
            var fullPath = Path.Combine(_basePath, path);
            var resolvedPath = Path.GetFullPath(fullPath);

            if (!resolvedPath.StartsWith(_basePath))
            {
                _logger.LogWarning($"Attempted unauthorized directory listing: {path}");
                return new List<string>();
            }

            if (!Directory.Exists(fullPath))
            {
                return new List<string>();
            }

            var files = Directory.GetFiles(fullPath)
                .Select(f => Path.GetFileName(f))
                .ToList();

            return files;
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error listing files in {path}: {ex.Message}");
            return new List<string>();
        }
    }
}
