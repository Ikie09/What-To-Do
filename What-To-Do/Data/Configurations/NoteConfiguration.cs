using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using What_To_Do.Domain.Entities;

namespace What_To_Do.Data.Configurations;

public class NoteConfiguration : IEntityTypeConfiguration<Note>
{
    public void Configure(EntityTypeBuilder<Note> entity)
    {
        entity.HasKey(n => n.Id);
        entity.Property(n => n.Title)
              .IsRequired()
              .HasMaxLength(255);
        entity.Property(n => n.CreatedAt)
              .HasDefaultValueSql("GETUTCDATE()");
        entity.Property(n => n.UpdatedAt)
              .HasDefaultValueSql("GETUTCDATE()");

        entity.HasOne(n => n.User)
              .WithMany()
              .HasForeignKey(n => n.UserId)
              .OnDelete(DeleteBehavior.Cascade);

        entity.HasQueryFilter(n => !n.IsDeleted);
    }
}
