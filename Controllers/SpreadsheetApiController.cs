using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using SocialCalc.Web.Models;
using SocialCalc.Web.Services;
using SocialCalc.Web.Services.Pdf;
using System.IO;

namespace SocialCalc.Web.Controllers;

[ApiController]
[Route("api/spreadsheet")]
[Authorize]
public class SpreadsheetApiController : ControllerBase
{
    private readonly ISheetService _sheetService;
    private readonly ILogger<SpreadsheetApiController> _logger;
    private readonly ISpreadsheetHtmlRenderer _htmlRenderer;
    private readonly IPdfRenderEngine _pdfEngine;
    private readonly IPdfJobQueue _pdfJobQueue;

    public SpreadsheetApiController(
        ISheetService sheetService,
        ISpreadsheetHtmlRenderer htmlRenderer,
        IPdfRenderEngine pdfEngine,
        IPdfJobQueue pdfJobQueue,
        ILogger<SpreadsheetApiController> logger)
    {
        _sheetService = sheetService;
        _htmlRenderer = htmlRenderer;
        _pdfEngine = pdfEngine;
        _pdfJobQueue = pdfJobQueue;
        _logger = logger;
    }

private bool TryGetCurrentUserId(out int userId)
{
    userId = 0;
    var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
    return !string.IsNullOrEmpty(userIdStr) && int.TryParse(userIdStr, out userId);
}

    [HttpPost("import")]
    [EnableRateLimiting("ApiPolicy")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Import([FromBody] SpreadsheetImportDto request)
    {
        int userId = 0;
        try
        {
            if (!TryGetCurrentUserId(out userId))
            {
                return Unauthorized();
            }

            if (request.Sheets == null || request.Sheets.Count == 0)
            {
                return BadRequest(new { success = false, message = "No sheets provided in payload" });
            }

            // Map incoming generic DTO to SocialCalc JSON/savestr format
            string socialCalcJson = MapToSocialCalcJson(request);

            Sheet? sheet;
            if (request.TargetSheetId.HasValue)
            {
                // Import into existing sheet
                sheet = await _sheetService.GetSheetAsync(request.TargetSheetId.Value, userId);
                if (sheet == null)
                {
                    return NotFound(new { success = false, message = "Target sheet not found" });
                }
                
                sheet.Data = socialCalcJson;
                sheet.UpdatedAt = DateTime.UtcNow;
                var success = await _sheetService.UpdateSheetAsync(sheet);
                if (!success)
                {
                    return StatusCode(500, new { success = false, message = "Failed to update sheet" });
                }
            }
            else
            {
                // Create a new sheet
                string safeFileName = string.IsNullOrWhiteSpace(request.FileName) ? "Untitled" : request.FileName;
                safeFileName = System.Text.RegularExpressions.Regex.Replace(safeFileName, @"[\\/<>:""|?*]", "_");
                if (safeFileName.Length > 200) safeFileName = safeFileName.Substring(0, 200);
                sheet = await _sheetService.SaveSheetAsync(userId, safeFileName, socialCalcJson);
                if (sheet == null)
                {
                    return StatusCode(500, new { success = false, message = "Failed to create new sheet" });
                }
            }

            return Ok(new { success = true, id = sheet.Id, message = "Import successful" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error importing spreadsheet via API for user {UserId}", userId == 0 ? "unknown" : userId.ToString());
            return StatusCode(500, new { success = false, message = "Error importing spreadsheet" });
        }
    }

    [HttpGet("export/{id}")]
    [EnableRateLimiting("ApiPolicy")]
    public async Task<IActionResult> Export(int id)
    {
        int userId = 0;
        try
        {
            if (!TryGetCurrentUserId(out userId))
            {
                return Unauthorized();
            }

            var sheet = await _sheetService.GetSheetAsync(id, userId);
            if (sheet == null)
            {
                return NotFound(new { success = false, message = "Sheet not found" });
            }

            // Parse the SocialCalc JSON wrapper
            JsonDocument jsonDoc;
            try
            {
                jsonDoc = JsonDocument.Parse(sheet.Data ?? "{}");
            }
            catch (JsonException ex)
            {
                _logger.LogError(ex, "Corrupt or malformed JSON data in sheet {SheetId}", id);
                return StatusCode(500, new { success = false, message = "Corrupt sheet data" });
            }

            using (jsonDoc)
            {
                var root = jsonDoc.RootElement;
                
                var dto = new SpreadsheetImportDto
                {
                    FileName = sheet.FileName
                };

                if (root.TryGetProperty("sheetArr", out var sheetArr))
                {
                    foreach (var sheetProp in sheetArr.EnumerateObject())
                    {
                        var sheetObj = sheetProp.Value;
                        string sheetName = sheetObj.TryGetProperty("name", out var nameProp) ? nameProp.GetString() ?? sheetProp.Name : sheetProp.Name;
                        string savestr = "";
                        
                        if (sheetObj.TryGetProperty("sheetstr", out var sheetstrObj) && 
                            sheetstrObj.TryGetProperty("savestr", out var savestrProp))
                        {
                            savestr = savestrProp.GetString() ?? "";
                        }
                        else if (sheetObj.TryGetProperty("savestr", out var sProp)) // Sometimes flat
                        {
                            savestr = sProp.GetString() ?? "";
                        }

                        if (!string.IsNullOrEmpty(savestr))
                        {
                            dto.Sheets.Add(ParseSocialCalcSaveStr(sheetName, savestr));
                        }
                    }
                }

                return Ok(dto);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error exporting spreadsheet via API for user {UserId}", userId == 0 ? "unknown" : userId.ToString());
            return StatusCode(500, new { success = false, message = "Error exporting spreadsheet" });
        }
    }

    [HttpPost("~/api/export/pdf")]
    [EnableRateLimiting("ApiPolicy")]
    public async Task<IActionResult> ExportPdf([FromBody] PdfExportRequest request)
    {
        int userId = 0;
        try
        {
            if (!TryGetCurrentUserId(out userId))
            {
                return Unauthorized();
            }

            SpreadsheetData dataToExport = new SpreadsheetData();

            if (request.SpreadsheetId.HasValue)
            {
                var sheet = await _sheetService.GetSheetAsync(request.SpreadsheetId.Value, userId);
                if (sheet == null)
                {
                    return NotFound(new { success = false, message = "Sheet not found" });
                }
                dataToExport.JsonData = sheet.Data;
                dataToExport.FileName = sheet.FileName;
            }
            else if (request.Data != null)
            {
                dataToExport = request.Data;
            }
            else
            {
                return BadRequest(new { success = false, message = "Must provide either SpreadsheetId or Data" });
            }

            // Phase 5: Size threshold check
            bool isLarge = (dataToExport.JsonData?.Length ?? 0) > 500_000; // 500KB threshold

            // Phase 2: Convert to HTML with repeating headers and potential watermark
            string html = await _htmlRenderer.RenderToHtmlAsync(dataToExport, request.PrintSettings);

            if (isLarge)
            {
                var jobId = Guid.NewGuid().ToString("N");
                var jobReq = new PdfJobRequest
                {
                    JobId = jobId,
                    HtmlContent = html,
                    PrintSettings = request.PrintSettings,
                    OriginalFileName = dataToExport.FileName ?? "export"
                };
                
                await _pdfJobQueue.QueueJobAsync(jobReq);
                return Accepted(new { success = true, jobId = jobId, message = "Job queued. Check status endpoint." });
            }

            // Phase 3 & 4: Render to PDF and stream
            var tempDir = Path.Combine(Path.GetTempPath(), "SocialCalcPdf");
            Directory.CreateDirectory(tempDir);
            var tempFilePath = Path.Combine(tempDir, $"export_{Guid.NewGuid():N}.pdf");

            await _pdfEngine.RenderHtmlToPdfFileAsync(html, tempFilePath, request.PrintSettings);

            var fs = new FileStream(tempFilePath, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, FileOptions.DeleteOnClose);
            var fileName = string.IsNullOrWhiteSpace(dataToExport.FileName) ? "export.pdf" : $"{dataToExport.FileName}.pdf";
            
            return new FileStreamResult(fs, "application/pdf")
            {
                FileDownloadName = fileName
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error exporting PDF via API");
            return StatusCode(500, new { success = false, message = "Error generating PDF" });
        }
    }

    [HttpGet("~/api/export/pdf/status/{jobId}")]
    public IActionResult GetPdfStatus(string jobId)
    {
        var state = _pdfJobQueue.GetJobState(jobId);
        if (state == null)
        {
            return NotFound(new { success = false, message = "Job not found" });
        }
        return Ok(new { success = true, jobId = state.JobId, status = state.Status.ToString(), error = state.ErrorMessage });
    }

    [HttpGet("~/api/export/pdf/download/{jobId}")]
    public IActionResult DownloadPdfJob(string jobId)
    {
        var state = _pdfJobQueue.GetJobState(jobId);
        if (state == null)
        {
            return NotFound(new { success = false, message = "Job not found" });
        }

        if (state.Status != PdfJobStatus.Completed)
        {
            return BadRequest(new { success = false, message = "Job is not completed yet" });
        }

        if (!System.IO.File.Exists(state.OutputFilePath))
        {
            return NotFound(new { success = false, message = "File not found on disk" });
        }

        var fs = new FileStream(state.OutputFilePath, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, FileOptions.DeleteOnClose);
        var fileName = string.IsNullOrWhiteSpace(state.OriginalFileName) ? "export.pdf" : $"{state.OriginalFileName}.pdf";
        
        return new FileStreamResult(fs, "application/pdf")
        {
            FileDownloadName = fileName
        };
    }

    internal SheetImportDto ParseSocialCalcSaveStr(string name, string savestr)
    {
        var sheet = new SheetImportDto { Name = name };
        var lines = savestr.Split('\n', StringSplitOptions.RemoveEmptyEntries);

        foreach (var line in lines)
        {
            var parts = line.Split(':');
            if (parts.Length < 2) continue;

            if (parts[0] == "cell" && parts.Length >= 4)
            {
                // cell:A1:vtf:n:10:SUM(...)
                string coord = parts[1];
                (int c, int r) = DecodeCoord(coord);

                string type = parts[2];
                if (type == "v" || type == "t" || type == "vtf" || type == "vtc")
                {
                    var cellDto = new CellImportDto { C = c, R = r };
                    
                    if (type == "v") // cell:A1:v:10
                    {
                        cellDto.T = "n";
                        if (double.TryParse(parts[3], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out double val)) cellDto.V = val;
                    }
                    else if (type == "t") // cell:A1:t:Hello
                    {
                        cellDto.T = "s";
                        cellDto.V = UnescapeString(string.Join(":", parts.Skip(3)));
                    }
                    else if (type == "vtf" || type == "vtc") // cell:A1:vtf:n:10:SUM(A1)
                    {
                        string fType = parts[3]; // n or t
                        cellDto.T = (fType == "n") ? "n" : "s";
                        
                        string valStr = parts[4];
                        if (fType == "n" && double.TryParse(valStr, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out double val)) cellDto.V = val;
                        else cellDto.V = UnescapeString(valStr);

                        if (type == "vtf" && parts.Length > 5)
                        {
                            cellDto.F = UnescapeString(string.Join(":", parts.Skip(5)));
                        }
                    }

                    sheet.Cells.Add(cellDto);
                }
                
                // Look for merges (rowspan / colspan)
                // Format: cell:A1:rowspan:2 or cell:A1:rowspan:2:colspan:2
                int rowSpan = 1;
                int colSpan = 1;
                
                for (int i = 4; i < parts.Length - 1; i++)
                {
                    if (parts[i] == "rowspan" && int.TryParse(parts[i+1], out int rs)) rowSpan = rs;
                    if (parts[i] == "colspan" && int.TryParse(parts[i+1], out int cs)) colSpan = cs;
                }

                if (rowSpan > 1 || colSpan > 1)
                {
                    sheet.Merges.Add(new MergeImportDto
                    {
                        S = new MergePointDto { R = r, C = c },
                        E = new MergePointDto { R = r + rowSpan - 1, C = c + colSpan - 1 }
                    });
                }
            }
            else if (parts[0] == "col" && parts.Length >= 4 && parts[2] == "w") // col:A:w:120
            {
                int c = DecodeColLetter(parts[1]);
                if (double.TryParse(parts[3], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out double w))
                {
                    sheet.Cols.Add(new ColRowImportDto { Index = c, Size = w });
                }
            }
            else if (parts[0] == "row" && parts.Length >= 4 && parts[2] == "h") // row:1:h:30
            {
                if (int.TryParse(parts[1], out int r) && double.TryParse(parts[3], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out double h))
                {
                    sheet.Rows.Add(new ColRowImportDto { Index = r - 1, Size = h });
                }
            }
        }

        return sheet;
    }

    private (int, int) DecodeCoord(string coord)
    {
        int col = 0;
        int row = 0;
        
        foreach (char c in coord)
        {
            if (char.IsLetter(c))
            {
                col = (col * 26) + (char.ToUpper(c) - 'A' + 1);
            }
            else if (char.IsDigit(c))
            {
                row = (row * 10) + (c - '0');
            }
        }
        
        return (col - 1, row - 1); // 0-indexed
    }

    private int DecodeColLetter(string col)
    {
        int c = 0;
        foreach (char ch in col)
        {
            if (char.IsLetter(ch))
            {
                c = (c * 26) + (char.ToUpper(ch) - 'A' + 1);
            }
        }
        return c - 1;
    }

    private string UnescapeString(string s)
    {
        if (string.IsNullOrEmpty(s)) return "";
        return s.Replace("\\c", ":").Replace("\\n", "\n").Replace("\\b", "\\");
    }

    internal string MapToSocialCalcJson(SpreadsheetImportDto dto)
    {
        var sheetArr = new Dictionary<string, object>();
        
        for (int i = 0; i < dto.Sheets.Count; i++)
        {
            var incomingSheet = dto.Sheets[i];
            string savestr = GenerateSaveStr(incomingSheet);
            
            sheetArr[incomingSheet.Name] = new {
                name = incomingSheet.Name,
                sheetstr = new {
                    savestr = savestr
                }
            };
        }

        var root = new {
            numsheets = dto.Sheets.Count,
            currentname = dto.Sheets.Count > 0 ? dto.Sheets[0].Name : "Sheet1",
            sheetArr = sheetArr,
            currentid = dto.Sheets.Count > 0 ? dto.Sheets[0].Name : "Sheet1"
        };

        return JsonSerializer.Serialize(root);
    }

    internal string GenerateSaveStr(SheetImportDto sheet)
    {
        var sb = new StringBuilder();
        sb.Append("version:1.5\n");
        
        int maxRow = 0;
        int maxCol = 0;

        // Group merges by start cell
        var mergesByCell = new Dictionary<(int r, int c), MergeImportDto>();
        foreach (var m in sheet.Merges)
        {
            mergesByCell[(m.S.R, m.S.C)] = m;
        }

        // Map cells
        foreach (var cell in sheet.Cells)
        {
            string scCol = EncodeCol(cell.C);
            int scRow = cell.R + 1;
            
            if (cell.R + 1 > maxRow) maxRow = cell.R + 1;
            if (cell.C + 1 > maxCol) maxCol = cell.C + 1;

            string valStr = "";
            
            // Does it have a formula?
            if (!string.IsNullOrEmpty(cell.F))
            {
                // SocialCalc uses 'vtf' for values with formats and formulas
                string fType = (cell.T == "n") ? "n" : "t";
                string v = "";
                if (cell.V != null)
                {
                    if (cell.V is JsonElement j && j.ValueKind == JsonValueKind.String)
                        v = j.GetString() ?? "";
                    else
                        v = cell.V.ToString() ?? "";
                }
                
                if (fType == "t") v = EscapeString(v);
                
                valStr = $"vtf:{fType}:{v}:{EscapeString(cell.F)}";
            }
            else
            {
                if (cell.T == "n" || cell.V is JsonElement jVal && jVal.ValueKind == JsonValueKind.Number)
                {
                    if (cell.V is JsonElement je && je.ValueKind == JsonValueKind.Number)
                    {
                        valStr = $"v:{je.GetDouble().ToString("R", System.Globalization.CultureInfo.InvariantCulture)}";
                    }
                    else if (cell.V != null)
                    {
                        valStr = $"v:{Convert.ToDouble(cell.V).ToString("R", System.Globalization.CultureInfo.InvariantCulture)}";
                    }
                }
                else if (cell.V != null)
                {
                    if (cell.V is JsonElement j && j.ValueKind == JsonValueKind.String)
                        valStr = $"t:{EscapeString(j.GetString() ?? "")}";
                    else
                        valStr = $"t:{EscapeString(cell.V.ToString() ?? "")}";
                }
            }

            // Check if this cell is the start of a merge
            string mergeStr = "";
            if (mergesByCell.TryGetValue((cell.R, cell.C), out var merge))
            {
                int rowSpan = merge.E.R - merge.S.R + 1;
                int colSpan = merge.E.C - merge.S.C + 1;
                if (rowSpan > 1) mergeStr += $":rowspan:{rowSpan}";
                if (colSpan > 1) mergeStr += $":colspan:{colSpan}";
                mergesByCell.Remove((cell.R, cell.C)); // handled
            }
            
            if (!string.IsNullOrEmpty(valStr))
            {
                sb.Append($"cell:{scCol}{scRow}:{valStr}{mergeStr}\n");
            }
            else if (!string.IsNullOrEmpty(mergeStr))
            {
                sb.Append($"cell:{scCol}{scRow}{mergeStr}\n");
            }
        }

        // Map remaining merges (empty cells with merges)
        foreach (var m in mergesByCell.Values)
        {
            string scCol = EncodeCol(m.S.C);
            int scRow = m.S.R + 1;
            int rowSpan = m.E.R - m.S.R + 1;
            int colSpan = m.E.C - m.S.C + 1;
            
            string mergeStr = "";
            if (rowSpan > 1) mergeStr += $":rowspan:{rowSpan}";
            if (colSpan > 1) mergeStr += $":colspan:{colSpan}";
            
            if (!string.IsNullOrEmpty(mergeStr))
            {
                sb.Append($"cell:{scCol}{scRow}{mergeStr}\n");
            }
        }
        
        // Map column widths
        foreach (var col in sheet.Cols)
        {
            string scCol = EncodeCol(col.Index);
            // SheetJS col width is approximately (pixels / 7) characters. 
            // We'll pass the raw size and convert to typical pixel width for SocialCalc if needed, 
            // but let's assume it's given in roughly the right units or pixels.
            // For simplicity, let's just output the 'w' attribute.
            sb.Append($"col:{scCol}:w:{(int)col.Size}\n");
        }

        // Map row heights
        foreach (var row in sheet.Rows)
        {
            int scRow = row.Index + 1;
            sb.Append($"row:{scRow}:h:{(int)row.Size}\n");
        }
        
        sb.Append($"sheet:c:{maxCol}:r:{maxRow}:tvf:1\n");
        
        return sb.ToString();
    }

    private string EscapeString(string s)
    {
        if (string.IsNullOrEmpty(s)) return "";
        return s.Replace("\\", "\\b").Replace(":", "\\c").Replace("\n", "\\n");
    }

    private string EncodeCol(int colIndex)
    {
        int temp;
        string letter = "";
        while (colIndex >= 0)
        {
            temp = colIndex % 26;
            letter = (char)(temp + 65) + letter;
            colIndex = (colIndex / 26) - 1;
        }
        return letter;
}
}
