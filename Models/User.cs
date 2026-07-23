using Microsoft.AspNetCore.Identity;
using System.ComponentModel.DataAnnotations;

namespace SocialCalc.Web.Models;

public class User : IdentityUser<int>
{
    [Required]
    [EmailAddress]
    public override string Email { get; set; } = null!;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? LastLogin { get; set; }

    public bool IsActive { get; set; } = true;

    // Navigation
    public virtual ICollection<Sheet> Sheets { get; set; } = new List<Sheet>();
    public virtual ICollection<PasswordResetToken> ResetTokens { get; set; } = new List<PasswordResetToken>();
    public virtual ICollection<PersonalAccessToken> ApiTokens { get; set; } = new List<PersonalAccessToken>();
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
    [MaxLength(5242880)]
    public string Data { get; set; } = "{}"; // JSON format

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public bool IsDeleted { get; set; } = false;

    // Navigation
    public virtual User User { get; set; } = null!;
}

public class PasswordResetToken
{
    [Key]
    public int Id { get; set; }

    [Required]
    public int UserId { get; set; }

    [Required]
    [StringLength(500)]
    public string Token { get; set; } = null!;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime ExpiresAt { get; set; }

    public bool IsUsed { get; set; } = false;

    // Navigation
    public virtual User User { get; set; } = null!;
}

public class PersonalAccessToken
{
    [Key]
    public int Id { get; set; }

    [Required]
    public int UserId { get; set; }

    [Required]
    [StringLength(100)]
    public string Name { get; set; } = "API Token";

    [Required]
    [StringLength(255)]
    public string TokenHash { get; set; } = null!;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? LastUsedAt { get; set; }

    // Navigation
    public virtual User User { get; set; } = null!;
}
