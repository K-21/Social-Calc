using System.Collections.Generic;

namespace SocialCalc.Web.Models
{
    public class SpreadsheetImportDto
    {
        public string FileName { get; set; } = "Imported Spreadsheet";
        public int? TargetSheetId { get; set; }
        public List<SheetImportDto> Sheets { get; set; } = new List<SheetImportDto>();
    }

    public class SheetImportDto
    {
        public string Name { get; set; } = "Sheet1";
        public List<CellImportDto> Cells { get; set; } = new List<CellImportDto>();
        public List<MergeImportDto> Merges { get; set; } = new List<MergeImportDto>();
        public List<ColRowImportDto> Cols { get; set; } = new List<ColRowImportDto>();
        public List<ColRowImportDto> Rows { get; set; } = new List<ColRowImportDto>();
    }

    public class CellImportDto
    {
        public int R { get; set; } // 0-indexed row
        public int C { get; set; } // 0-indexed column
        public object? V { get; set; } // Value (number or string)
        public string? T { get; set; } // Type: "n", "s", "b", etc.
        public string? F { get; set; } // Formula string if present
    }

    public class MergeImportDto
    {
        public MergePointDto S { get; set; } = new MergePointDto();
        public MergePointDto E { get; set; } = new MergePointDto();
    }

    public class MergePointDto
    {
        public int R { get; set; }
        public int C { get; set; }
    }

    public class ColRowImportDto
    {
        public int Index { get; set; } // 0-indexed column or row
        public double Size { get; set; } // Width or Height
    }
}
