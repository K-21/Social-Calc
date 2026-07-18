namespace SocialCalc.Web.Models;

public class SpreadsheetData
{
    public string JsonData { get; set; } = "{}";
    public string FileName { get; set; } = "";

    public static SpreadsheetData FromSheet(Sheet sheet)
    {
        return new SpreadsheetData
        {
            FileName = sheet.FileName,
            JsonData = sheet.Data
        };
    }
}
