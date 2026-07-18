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

    public DbSet<Sheet> Sheets { get; set; } = null!;

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
        });

        // Sheet Configuration
        modelBuilder.Entity<Sheet>(entity =>
        {
            entity.HasQueryFilter(s => !s.IsDeleted);
            entity.HasIndex(s => new { s.UserId, s.IsDeleted, s.UpdatedAt });
            entity.HasIndex(s => s.CreatedAt);
            entity.Property(s => s.Data); // Allow large JSON data
        });
    }
}

