using SocialCalc.Web.Models;

namespace SocialCalc.Web.Services;

public abstract class SpreadsheetServiceBase : ISpreadsheetService
{
    protected readonly ILogger _logger;

    protected SpreadsheetServiceBase(ILogger logger)
    {
        _logger = logger;
    }

    public abstract Task<byte[]> ExportAsync(SpreadsheetData data, string format);
    public abstract Task<SpreadsheetData> ImportAsync(Stream fileStream, string format);

    public async Task<bool> IsValidExcelFileAsync(Stream fileStream, string fileName)
    {
        try
        {
            fileStream.Seek(0, SeekOrigin.Begin);
            var buffer = new byte[512];
            int bytesRead = await fileStream.ReadAsync(buffer, 0, 512);

            if (bytesRead < 4) return false;

            var isZipBased = buffer[0] == 0x50 && buffer[1] == 0x4B; // XLSX or ODS
            var isXls = buffer[0] == 0xD0 && buffer[1] == 0xCF; // Legacy XLS
            
            // For CSV/Text, ensure there are no null bytes in the sample
            bool hasNullByte = false;
            for (int i = 0; i < bytesRead; i++)
            {
                if (buffer[i] == 0x00)
                {
                    hasNullByte = true;
                    break;
                }
            }
            var extension = Path.GetExtension(fileName)?.ToLowerInvariant();
            var isValidCsvExt = extension == ".csv" || extension == ".tsv" || extension == ".txt";
            var isCsvOrText = !hasNullByte && isValidCsvExt;

            fileStream.Seek(0, SeekOrigin.Begin);
            return isZipBased || isXls || isCsvOrText;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error validating Excel file");
            return false;
        }
    }
}
