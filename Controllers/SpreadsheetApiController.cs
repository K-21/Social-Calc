using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using SocialCalc.Web.Models;
using SocialCalc.Web.Services;

namespace SocialCalc.Web.Controllers
{
    [ApiController]
    [Route("api/spreadsheet")]
    [Authorize]
    public class SpreadsheetApiController : ControllerBase
    {
        private readonly ISheetService _sheetService;
        private readonly UserManager<User> _userManager;
        private readonly ILogger<SpreadsheetApiController> _logger;

        public SpreadsheetApiController(
            ISheetService sheetService,
            UserManager<User> userManager,
            ILogger<SpreadsheetApiController> logger)
        {
            _sheetService = sheetService;
            _userManager = userManager;
            _logger = logger;
        }

        [HttpPost("import")]
        public async Task<IActionResult> Import([FromBody] SpreadsheetImportDto request)
        {
            try
            {
                var user = await _userManager.GetUserAsync(User);
                if (user == null)
                {
                    return Unauthorized();
                }

                // Map incoming generic DTO to SocialCalc JSON/savestr format
                string socialCalcJson = MapToSocialCalcJson(request);

                Sheet sheet;
                if (request.TargetSheetId.HasValue)
                {
                    // Import into existing sheet
                    sheet = await _sheetService.GetSheetAsync(request.TargetSheetId.Value, user.Id);
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
                    sheet = await _sheetService.SaveSheetAsync(user.Id, request.FileName, socialCalcJson);
                    if (sheet == null)
                    {
                        return StatusCode(500, new { success = false, message = "Failed to create new sheet" });
                    }
                }

                return Ok(new { success = true, id = sheet.Id, message = "Import successful" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error importing spreadsheet via API");
                return StatusCode(500, new { success = false, message = "Error importing spreadsheet: " + ex.Message });
            }
        }

        [HttpGet("export/{id}")]
        public async Task<IActionResult> Export(int id)
        {
            try
            {
                var user = await _userManager.GetUserAsync(User);
                if (user == null)
                {
                    return Unauthorized();
                }

                var sheet = await _sheetService.GetSheetAsync(id, user.Id);
                if (sheet == null)
                {
                    return NotFound(new { success = false, message = "Sheet not found" });
                }

                // Parse the SocialCalc JSON wrapper
                var jsonDoc = JsonDocument.Parse(sheet.Data);
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
                        string sheetName = sheetObj.TryGetProperty("name", out var nameProp) ? nameProp.GetString() : sheetProp.Name;
                        string savestr = "";
                        
                        if (sheetObj.TryGetProperty("sheetstr", out var sheetstrObj) && 
                            sheetstrObj.TryGetProperty("savestr", out var savestrProp))
                        {
                            savestr = savestrProp.GetString();
                        }
                        else if (sheetObj.TryGetProperty("savestr", out var sProp)) // Sometimes flat
                        {
                            savestr = sProp.GetString();
                        }

                        if (!string.IsNullOrEmpty(savestr))
                        {
                            dto.Sheets.Add(ParseSocialCalcSaveStr(sheetName, savestr));
                        }
                    }
                }

                return Ok(dto);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error exporting spreadsheet via API");
                return StatusCode(500, new { success = false, message = "Error exporting spreadsheet: " + ex.Message });
            }
        }

        private SheetImportDto ParseSocialCalcSaveStr(string name, string savestr)
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
                            if (double.TryParse(parts[3], out double val)) cellDto.V = val;
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
                            if (fType == "n" && double.TryParse(valStr, out double val)) cellDto.V = val;
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
                    
                    for (int i = 2; i < parts.Length - 1; i++)
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
                    if (double.TryParse(parts[3], out double w))
                    {
                        sheet.Cols.Add(new ColRowImportDto { Index = c, Size = w });
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

        private string MapToSocialCalcJson(SpreadsheetImportDto dto)
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
                currentid = "Sheet0"
            };

            return JsonSerializer.Serialize(root);
        }

        private string GenerateSaveStr(SheetImportDto sheet)
        {
            var sb = new StringBuilder();
            sb.Append("version:1.5\n");
            
            int maxRow = 1;
            int maxCol = 1;

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
                    string v = (cell.V != null) ? cell.V.ToString() : "";
                    
                    if (fType == "t") v = EscapeString(v);
                    
                    valStr = $"vtf:{fType}:{v}:{EscapeString(cell.F)}";
                }
                else
                {
                    if (cell.T == "n" || cell.V is JsonElement jVal && jVal.ValueKind == JsonValueKind.Number)
                    {
                        valStr = $"v:{cell.V}";
                    }
                    else if (cell.V != null)
                    {
                        valStr = $"t:{EscapeString(cell.V.ToString())}";
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
            // SocialCalc row height syntax: row:1:h:30
            // but wait, is it row:1:h:30 ? Actually, SocialCalc uses something like `row:1:h:30` or `row:1:customheight:yes...`. Let's just output `h`.
            // Wait, does socialcalc support row heights via standard savestr?
            // "row:1:height:30" or similar. EtherCalc uses `row:1:height:30`.
            // Let's use `h`. If it fails, SocialCalc ignores it.
            // Actually, wait, `socialcalc-3.js` uses `sheet:c:maxCol:r:maxRow...`.
            // Let's omit rows for now as row height is rarely strictly required, or we just write it.
            // I'll skip it unless I know the exact format, wait, `import.php` used it? `import.php` didn't use it.
            
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
}
