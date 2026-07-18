using System.Threading.Tasks;

namespace SocialCalc.Web.Services.Pdf;

public interface IPdfRenderEngine
{
    Task<byte[]> RenderHtmlToPdfAsync(string html, PdfRenderOptions? options = null);
    Task RenderHtmlToPdfFileAsync(string html, string tempFilePath, PdfRenderOptions? options = null);
}
