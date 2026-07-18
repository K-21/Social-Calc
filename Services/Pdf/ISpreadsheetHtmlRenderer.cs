using System.Threading.Tasks;
using SocialCalc.Web.Models;

namespace SocialCalc.Web.Services.Pdf;

public interface ISpreadsheetHtmlRenderer
{
    Task<string> RenderToHtmlAsync(SpreadsheetData data, PdfRenderOptions? options = null);
}
