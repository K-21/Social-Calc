using System.Text;
using System.Threading.Tasks;
using SocialCalc.Web.Models;
using System.Text.RegularExpressions;

namespace SocialCalc.Web.Services.Pdf;

public class SpreadsheetHtmlRenderer : ISpreadsheetHtmlRenderer
{
    private readonly ISpreadsheetService _spreadsheetService;

    public SpreadsheetHtmlRenderer(ISpreadsheetService spreadsheetService)
    {
        _spreadsheetService = spreadsheetService;
    }

    public async Task<string> RenderToHtmlAsync(SpreadsheetData data, PdfRenderOptions? options = null)
    {
        var htmlBytes = await _spreadsheetService.ExportAsync(data, "Html");
        if (htmlBytes == null || htmlBytes.Length == 0)
        {
            return string.Empty;
        }

        var html = Encoding.UTF8.GetString(htmlBytes);

        // PhpSpreadsheet typically outputs a single <tbody> with all rows.
        // We want repeating headers, so we wrap the first <tr> in a <thead>.
        var tbodyTrRegex = new Regex(@"(<tbody>\s*)(<tr.*?>.*?</tr>)", RegexOptions.Singleline | RegexOptions.IgnoreCase);
        if (tbodyTrRegex.IsMatch(html))
        {
            html = tbodyTrRegex.Replace(html, "$1<thead>$2</thead>", 1);
        }

        // Add the CSS to ensure headers repeat on every printed page, and make the table fill the page properly.
        var styleToInject = @"<style>
            @page { size: auto; }
            body { font-family: Arial, sans-serif; margin: 0; padding: 0; }
            thead { display: table-header-group; }
            tr { page-break-inside: avoid; }
            table { 
                width: 100% !important; 
                max-width: 100% !important; 
                border-collapse: collapse !important; 
                table-layout: auto !important; 
                margin: 0 auto; 
            }
            td, th { 
                word-wrap: break-word !important; 
                white-space: pre-wrap !important; 
                max-width: 300px;
                border: 1px solid #ddd !important;
                padding: 4px !important;
            }";
        
        // Add watermark if specified
        if (!string.IsNullOrEmpty(options?.WatermarkText))
        {
            styleToInject += $@"
                body::after {{
                    content: '{options.WatermarkText}';
                    position: fixed;
                    top: 50%;
                    left: 50%;
                    transform: translate(-50%, -50%) rotate(-45deg);
                    font-size: 100px;
                    color: rgba(200, 200, 200, 0.3);
                    z-index: -1;
                    pointer-events: none;
                    white-space: nowrap;
                }}";
        }
        
        styleToInject += "</style>";

        if (html.Contains("</head>"))
        {
            html = html.Replace("</head>", styleToInject + "</head>");
        }
        else
        {
            html = styleToInject + html;
        }

        return html;
    }
}
