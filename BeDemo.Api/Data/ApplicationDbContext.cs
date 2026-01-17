using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using BeDemo.Api.Models;

namespace BeDemo.Api.Data;

public class ApplicationDbContext : IdentityDbContext<ApplicationUser>
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
    }

    public DbSet<Face> Faces { get; set; } = null!;
    public DbSet<Page> Pages { get; set; } = null!;
    public DbSet<PageType> PageTypes { get; set; } = null!;

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        // Configure Face entity
        builder.Entity<Face>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.Index).IsUnique();
            entity.Property(e => e.Index).IsRequired().HasMaxLength(100);
            entity.Property(e => e.Title).IsRequired().HasMaxLength(200);
            entity.Property(e => e.Description).HasMaxLength(1000);
            entity.Property(e => e.Color).HasMaxLength(50);
            entity.Property(e => e.CreatedAt).IsRequired();
            
            // One-to-many relationship: Face -> Pages
            entity.HasMany(e => e.Pages)
                  .WithOne(p => p.Face)
                  .HasForeignKey(p => p.FaceId)
                  .OnDelete(DeleteBehavior.Cascade); // If Face is deleted, delete all Pages
        });

        // Configure Page entity
        builder.Entity<Page>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).IsRequired().HasMaxLength(200);
            entity.Property(e => e.Description).HasMaxLength(1000);
            entity.Property(e => e.Path).IsRequired().HasMaxLength(500);
            entity.Property(e => e.Index).IsRequired();
            entity.Property(e => e.CreatedAt).IsRequired();
            
            // Foreign key relationship to Face
            entity.HasIndex(e => e.FaceId);
            
            // Many-to-one relationship: Page -> PageType
            entity.HasOne(e => e.PageType)
                  .WithMany(pt => pt.Pages)
                  .HasForeignKey(e => e.PageTypeId)
                  .OnDelete(DeleteBehavior.Restrict); // Prevent deletion if pages exist
        });

        // Configure PageType entity
        builder.Entity<PageType>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.Index).IsUnique();
            entity.Property(e => e.Index).IsRequired().HasMaxLength(100);
            entity.Property(e => e.CreatedAt).IsRequired();
            
            // One-to-many relationship: PageType -> Pages
            entity.HasMany(e => e.Pages)
                  .WithOne(p => p.PageType)
                  .HasForeignKey(p => p.PageTypeId)
                  .OnDelete(DeleteBehavior.Restrict); // Prevent deletion if pages exist
        });
    }
}
