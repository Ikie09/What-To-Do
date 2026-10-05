using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using What_To_Do.Domain.Entities;

namespace What_To_Do.Data.Configurations;

public class MeetingConfiguration : IEntityTypeConfiguration<Meeting>
{
    public void Configure(EntityTypeBuilder<Meeting> entity)
    {
        entity.HasKey(m => m.Id);
        entity.Property(m => m.Title)
              .IsRequired()
              .HasMaxLength(255);
        entity.Property(m => m.Location)
              .HasMaxLength(255);
        entity.Property(m => m.CreatedAt)
              .HasDefaultValueSql("GETUTCDATE()");
        entity.Property(m => m.UpdatedAt)
              .HasDefaultValueSql("GETUTCDATE()");

        entity.HasOne(m => m.User)
              .WithMany()
              .HasForeignKey(m => m.UserId)
              .OnDelete(DeleteBehavior.Cascade);

        entity.HasQueryFilter(m => !m.IsDeleted);
    }
}
