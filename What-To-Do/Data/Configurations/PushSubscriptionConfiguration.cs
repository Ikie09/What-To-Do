using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using What_To_Do.Domain.Entities;

namespace What_To_Do.Data.Configurations;

public class PushSubscriptionConfiguration : IEntityTypeConfiguration<PushSubscription>
{
    public void Configure(EntityTypeBuilder<PushSubscription> entity)
    {
        entity.HasKey(s => s.Id);
        entity.Property(s => s.Endpoint).IsRequired().HasMaxLength(2000);
        entity.Property(s => s.EndpointHash).IsRequired().HasColumnType("char(64)");
        entity.Property(s => s.P256dh).IsRequired().HasMaxLength(200);
        entity.Property(s => s.Auth).IsRequired().HasMaxLength(100);
        entity.Property(s => s.TimeZone).IsRequired().HasMaxLength(64);
        entity.Property(s => s.UserAgent).HasMaxLength(300);
        entity.Property(s => s.CreatedAt).HasDefaultValueSql("GETUTCDATE()");

        entity.HasOne(s => s.User)
              .WithMany()
              .HasForeignKey(s => s.UserId)
              .OnDelete(DeleteBehavior.Cascade);

        entity.HasIndex(s => s.EndpointHash).IsUnique();
        entity.HasIndex(s => s.UserId);
    }
}

public class SentPushReminderConfiguration : IEntityTypeConfiguration<SentPushReminder>
{
    public void Configure(EntityTypeBuilder<SentPushReminder> entity)
    {
        entity.HasKey(r => r.Id);
        entity.Property(r => r.Key).IsRequired().HasMaxLength(120);
        entity.Property(r => r.SentAt).HasDefaultValueSql("GETUTCDATE()");

        entity.HasOne(r => r.Subscription)
              .WithMany()
              .HasForeignKey(r => r.SubscriptionId)
              .OnDelete(DeleteBehavior.Cascade);

        entity.HasIndex(r => new { r.SubscriptionId, r.Key }).IsUnique();
        entity.HasIndex(r => r.SentAt);
    }
}
