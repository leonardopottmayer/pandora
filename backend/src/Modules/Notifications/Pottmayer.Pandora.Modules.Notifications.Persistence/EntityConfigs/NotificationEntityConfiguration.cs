using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Pottmayer.Pandora.Modules.Notifications.Abstractions;
using Pottmayer.Pandora.Modules.Notifications.Domain.Aggregates;
using Pottmayer.Pandora.Modules.Notifications.Domain.ValueObjects;
using Pottmayer.Pandora.Shared.Persistence.ValueConverters;

namespace Pottmayer.Pandora.Modules.Notifications.Persistence.EntityConfigs;

internal sealed class NotificationEntityConfiguration : IEntityTypeConfiguration<Notification>
{
    public void Configure(EntityTypeBuilder<Notification> builder)
    {
        builder.ToTable("not001_notification", NotificationsModule.Schema);

        builder.HasKey(n => n.Id)
               .HasName("pk_not001");

        builder.Property(n => n.Id)
               .HasColumnName("id")
               .ValueGeneratedNever();

        builder.Property(n => n.Channel)
               .HasColumnName("channel")
               .HasConversion(c => c.Value, v => Channel.FromValue(v))
               .HasMaxLength(20)
               .IsRequired();

        builder.Property(n => n.Recipient)
               .HasColumnName("recipient")
               .HasConversion(new EmailValueConverter())
               .HasMaxLength(255)
               .IsRequired();

        builder.Property(n => n.TemplateKey)
               .HasColumnName("template_key")
               .HasConversion(k => k.Value, v => TemplateKey.FromValue(v))
               .HasMaxLength(100)
               .IsRequired();

        builder.Property(n => n.Locale)
               .HasColumnName("locale")
               .HasMaxLength(10)
               .IsRequired();

        builder.Property(n => n.Payload)
               .HasColumnName("payload")
               .HasColumnType("jsonb")
               .IsRequired();

        builder.Property(n => n.Subject)
               .HasColumnName("subject")
               .HasMaxLength(255)
               .IsRequired();

        builder.Property(n => n.Body)
               .HasColumnName("body")
               .HasColumnType("text")
               .IsRequired();

        builder.Property(n => n.IsHtml)
               .HasColumnName("is_html")
               .IsRequired();

        builder.Property(n => n.Status)
               .HasColumnName("status")
               .HasConversion<string>()
               .HasMaxLength(20)
               .IsRequired();

        builder.Property(n => n.AttemptCount)
               .HasColumnName("attempt_count")
               .IsRequired();

        builder.Property(n => n.MaxAttempts)
               .HasColumnName("max_attempts")
               .IsRequired();

        builder.Property(n => n.NextAttemptAt)
               .HasColumnName("next_attempt_at")
               .IsRequired();

        builder.Property(n => n.LastError)
               .HasColumnName("last_error")
               .HasColumnType("text");

        builder.Property(n => n.Provider)
               .HasColumnName("provider")
               .HasMaxLength(100);

        builder.Property(n => n.ProviderMessageId)
               .HasColumnName("provider_message_id")
               .HasMaxLength(255);

        builder.Property(n => n.CorrelationId)
               .HasColumnName("correlation_id")
               .IsRequired();

        builder.HasIndex(n => n.CorrelationId)
               .HasDatabaseName("uq_not001_correlation_id")
               .IsUnique();

        builder.HasIndex(n => new { n.Status, n.NextAttemptAt })
               .HasDatabaseName("ix_not001_status_next_attempt_at");

        builder.Property(n => n.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(n => n.CreatedBy).HasColumnName("created_by");
        builder.Property(n => n.UpdatedAt).HasColumnName("updated_at");
        builder.Property(n => n.UpdatedBy).HasColumnName("updated_by");
    }
}
