using SocialCalc.Web.Services.Pdf;

namespace SocialCalc.Web.Models;

public class PdfExportRequest
{
    public int? SpreadsheetId { get; set; }
    public SpreadsheetData? Data { get; set; }
    public PdfRenderOptions? PrintSettings { get; set; }
}
