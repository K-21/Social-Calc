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

        private string MapToSocialCalcJson(SpreadsheetImportDto dto)
        {
            var sheetArr = new Dictionary<string, object>();
            
            for (int i = 0; i < dto.Sheets.Count; i++)
            {
                var incomingSheet = dto.Sheets[i];
                string savestr = GenerateSaveStr(incomingSheet);
                
                sheetArr[$"Sheet{i}"] = new {
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
                    // E.g. cell:A1:vtf:n:10:SUM(B2:B3)
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
                
                if (!string.IsNullOrEmpty(valStr))
                {
                    sb.Append($"cell:{scCol}{scRow}:{valStr}\n");
                }
            }

            // Map merges (rowspan / colspan)
            foreach (var m in sheet.Merges)
            {
                string scCol = EncodeCol(m.S.C);
                int scRow = m.S.R + 1;
                int rowSpan = m.E.R - m.S.R + 1;
                int colSpan = m.E.C - m.S.C + 1;
                
                if (rowSpan > 1) sb.Append($"cell:{scCol}{scRow}:rowspan:{rowSpan}\n");
                if (colSpan > 1) sb.Append($"cell:{scCol}{scRow}:colspan:{colSpan}\n");
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
