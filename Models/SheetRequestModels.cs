namespace SocialCalc.Web.Models;

public class SaveSheetRequest
{
    public string Data { get; set; } = null!;
}

public class RenameSheetRequest
{
    public string FileName { get; set; } = null!;
}

public class CreateSheetRequest
{
    public string FileName { get; set; } = "";
}
