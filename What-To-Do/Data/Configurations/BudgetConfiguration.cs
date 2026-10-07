using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using What_To_Do.Domain.Entities;

namespace What_To_Do.Data.Configurations;

public class BudgetConfiguration : IEntityTypeConfiguration<Budget>
{
    public void Configure(EntityTypeBuilder<Budget> entity)
    {
        entity.HasKey(b => b.Id);
        entity.Property(b => b.Category)
              .IsRequired()
              .HasMaxLength(100);
        entity.Property(b => b.MonthlyLimit)
              .HasPrecision(18, 2);
        entity.Property(b => b.CreatedAt)
              .HasDefaultValueSql("GETUTCDATE()");
        entity.Property(b => b.UpdatedAt)
              .HasDefaultValueSql("GETUTCDATE()");

        entity.HasOne(b => b.User)
              .WithMany()
              .HasForeignKey(b => b.UserId)
              .OnDelete(DeleteBehavior.Cascade);

        entity.HasQueryFilter(b => !b.IsDeleted);
    }
}
