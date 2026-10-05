using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using What_To_Do.Domain.Entities;

namespace What_To_Do.Data.Configurations;

public class CategoryConfiguration : IEntityTypeConfiguration<Category>
{
    public void Configure(EntityTypeBuilder<Category> entity)
    {
        entity.HasKey(c => c.Id);
        entity.Property(c => c.Name)
              .IsRequired()
              .HasMaxLength(100);
        entity.Property(c => c.Color)
              .HasMaxLength(7);
        entity.Property(c => c.CreatedAt)
              .HasDefaultValueSql("GETUTCDATE()");
        entity.Property(c => c.UpdatedAt)
              .HasDefaultValueSql("GETUTCDATE()");

        entity.HasOne(c => c.User)
              .WithMany()
              .HasForeignKey(c => c.UserId)
              .OnDelete(DeleteBehavior.Cascade);

        entity.HasQueryFilter(c => !c.IsDeleted);

        // System defaults every user sees (UserId left null).
        entity.HasData(
            new Category { Id = 1, Name = "Work", Color = "#4F46E5", CreatedAt = new DateTime(2026, 1, 1) },
            new Category { Id = 2, Name = "Personal", Color = "#10B981", CreatedAt = new DateTime(2026, 1, 1) },
            new Category { Id = 3, Name = "Shopping", Color = "#F59E0B", CreatedAt = new DateTime(2026, 1, 1) },
            new Category { Id = 4, Name = "Health", Color = "#EF4444", CreatedAt = new DateTime(2026, 1, 1) }
        );
    }
}
