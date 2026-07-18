namespace SocialCalc.Web.Services.Pdf;

public class PdfRenderOptions
{
    public string PageSize { get; set; } = "A4";
    public bool Landscape { get; set; } = false;
    public string MarginTop { get; set; } = "1cm";
    public string MarginRight { get; set; } = "1cm";
    public string MarginBottom { get; set; } = "1cm";
    public string MarginLeft { get; set; } = "1cm";
    public bool DisplayHeaderFooter { get; set; } = false;
    public string HeaderTemplate { get; set; } = string.Empty;
    public string FooterTemplate { get; set; } = string.Empty;
    public double Scale { get; set; } = 1.0;
    public bool PrintBackground { get; set; } = true;
    public string WatermarkText { get; set; } = string.Empty;
}
