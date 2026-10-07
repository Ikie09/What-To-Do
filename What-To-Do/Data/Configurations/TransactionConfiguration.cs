using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using What_To_Do.Domain.Entities;
using What_To_Do.Domain.Enums;

namespace What_To_Do.Data.Configurations;

public class TransactionConfiguration : IEntityTypeConfiguration<Transaction>
{
    public void Configure(EntityTypeBuilder<Transaction> entity)
    {
        entity.HasKey(t => t.Id);
        entity.Property(t => t.Type)
              .HasConversion<string>()
              .HasMaxLength(10);
        entity.Property(t => t.Amount)
              .HasPrecision(18, 2);
        entity.Property(t => t.Category)
              .IsRequired()
              .HasMaxLength(100);
        entity.Property(t => t.Note)
              .HasMaxLength(500);
        entity.Property(t => t.Date)
              .HasColumnType("date");
        entity.Property(t => t.CreatedAt)
              .HasDefaultValueSql("GETUTCDATE()");
        entity.Property(t => t.UpdatedAt)
              .HasDefaultValueSql("GETUTCDATE()");

        entity.HasOne(t => t.User)
              .WithMany()
              .HasForeignKey(t => t.UserId)
              .OnDelete(DeleteBehavior.Cascade);

        entity.HasIndex(t => new { t.UserId, t.Date });
        entity.HasQueryFilter(t => !t.IsDeleted);
    }
}
