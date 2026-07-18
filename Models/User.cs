using Microsoft.AspNetCore.Identity;
using System.ComponentModel.DataAnnotations;

namespace SocialCalc.Web.Models;

public class User : IdentityUser<int>
{
    [Required]
    [EmailAddress]
    public override string? Email { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? LastLogin { get; set; }

    public bool IsActive { get; set; } = true;

    // Navigation
    public virtual ICollection<Sheet> Sheets { get; set; } = new List<Sheet>();
}

public class Sheet
{
    [Key]
    public int Id { get; set; }

    [Required]
    public int UserId { get; set; }

    [Required]
    [StringLength(255)]
    public string FileName { get; set; } = null!;

    [Required]
    public string Data { get; set; } = "{}"; // JSON format, unbounded text; size enforced at app layer

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public bool IsDeleted { get; set; } = false;

    // Navigation
    public virtual User User { get; set; } = null!;
}

