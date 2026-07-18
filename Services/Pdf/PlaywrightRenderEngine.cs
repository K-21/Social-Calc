using System;
using System.Threading.Tasks;
using Microsoft.Playwright;

namespace SocialCalc.Web.Services.Pdf;

public class PlaywrightRenderEngine : IPdfRenderEngine
{
    private PagePdfOptions MapOptions(PdfRenderOptions? options, string? path = null)
    {
        var pdfOptions = new PagePdfOptions
        {
            Format = options?.PageSize ?? "A4",
            Landscape = options?.Landscape ?? false,
            PrintBackground = options?.PrintBackground ?? true,
            Scale = (float)(options?.Scale ?? 1.0),
            DisplayHeaderFooter = options?.DisplayHeaderFooter ?? false
        };

        if (options != null)
        {
            pdfOptions.Margin = new Microsoft.Playwright.Margin
            {
                Top = options.MarginTop,
                Right = options.MarginRight,
                Bottom = options.MarginBottom,
                Left = options.MarginLeft
            };
            
            if (options.DisplayHeaderFooter)
            {
                pdfOptions.HeaderTemplate = string.IsNullOrEmpty(options.HeaderTemplate) ? "<div></div>" : options.HeaderTemplate;
                pdfOptions.FooterTemplate = string.IsNullOrEmpty(options.FooterTemplate) ? "<div></div>" : options.FooterTemplate;
            }
        }

        if (!string.IsNullOrEmpty(path))
        {
            pdfOptions.Path = path;
        }

        return pdfOptions;
    }

    public async Task<byte[]> RenderHtmlToPdfAsync(string html, PdfRenderOptions? options = null)
    {
        using var playwright = await Playwright.CreateAsync();
        await using var browser = await playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions { Headless = true });
        var page = await browser.NewPageAsync();
        
        await page.SetContentAsync(html, new PageSetContentOptions { WaitUntil = WaitUntilState.NetworkIdle });
        
        var pdfOptions = MapOptions(options);
        return await page.PdfAsync(pdfOptions);
    }

    public async Task RenderHtmlToPdfFileAsync(string html, string tempFilePath, PdfRenderOptions? options = null)
    {
        using var playwright = await Playwright.CreateAsync();
        await using var browser = await playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions { Headless = true });
        var page = await browser.NewPageAsync();
        
        await page.SetContentAsync(html, new PageSetContentOptions { WaitUntil = WaitUntilState.NetworkIdle });
        
        var pdfOptions = MapOptions(options, tempFilePath);
        await page.PdfAsync(pdfOptions);
    }
}
