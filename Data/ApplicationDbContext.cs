using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using SocialCalc.Web.Models;

namespace SocialCalc.Web.Data;

public class ApplicationDbContext : IdentityDbContext<User, IdentityRole<int>, int>
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options)
    {
    }

    public DbSet<Sheet> Sheets { get; set; }
    public DbSet<PasswordResetToken> PasswordResetTokens { get; set; }
    public DbSet<PersonalAccessToken> PersonalAccessTokens { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // User Configuration
        modelBuilder.Entity<User>(entity =>
        {
            entity.HasMany(u => u.Sheets)
                .WithOne(s => s.User)
                .HasForeignKey(s => s.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasMany(u => u.ResetTokens)
                .WithOne(t => t.User)
                .HasForeignKey(t => t.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // Sheet Configuration
        modelBuilder.Entity<Sheet>(entity =>
        {
            entity.HasIndex(s => s.UserId);
            entity.HasIndex(s => s.CreatedAt);
            entity.Property(s => s.Data); // Allow large JSON data
        });

        // PasswordResetToken Configuration
        modelBuilder.Entity<PasswordResetToken>(entity =>
        {
            entity.HasIndex(t => t.Token).IsUnique();
            entity.HasIndex(t => t.ExpiresAt);
        });
    }
}
