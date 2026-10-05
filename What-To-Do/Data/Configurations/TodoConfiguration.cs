using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using What_To_Do.Domain.Entities;
using What_To_Do.Domain.Enums;

namespace What_To_Do.Data.Configurations;

public class TodoConfiguration : IEntityTypeConfiguration<Todo>
{
    public void Configure(EntityTypeBuilder<Todo> entity)
    {
        entity.HasKey(t => t.Id);
        entity.Property(t => t.Title)
              .IsRequired()
              .HasMaxLength(255);
        entity.Property(t => t.Priority)
              .HasConversion<string>()
              .HasMaxLength(10);
        entity.Property(t => t.Status)
              .HasConversion<string>()
              .HasMaxLength(12)
              .HasDefaultValue(TodoStatus.Todo)
              .HasSentinel((TodoStatus)0);
        entity.Property(t => t.CreatedAt)
              .HasColumnType("DATE")
              .HasDefaultValueSql("GETUTCDATE()");
        entity.Property(t => t.UpdatedAt)
              .HasDefaultValueSql("GETUTCDATE()");

        entity.HasOne(t => t.Category)
              .WithMany(c => c.Todos)
              .HasForeignKey(t => t.CategoryId)
              .OnDelete(DeleteBehavior.SetNull);

        entity.HasOne(t => t.User)
              .WithMany()
              .HasForeignKey(t => t.UserId)
              .OnDelete(DeleteBehavior.Restrict);

        entity.HasQueryFilter(t => !t.IsDeleted);
    }
}
