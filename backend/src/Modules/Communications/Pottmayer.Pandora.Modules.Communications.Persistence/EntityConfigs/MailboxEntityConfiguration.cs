using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Pottmayer.Pandora.Modules.Communications.Abstractions;
using Pottmayer.Pandora.Modules.Communications.Domain.Entities;
using Pottmayer.Pandora.Modules.Communications.Domain.ValueObjects;

namespace Pottmayer.Pandora.Modules.Communications.Persistence.EntityConfigs;

internal sealed class MailboxEntityConfiguration : IEntityTypeConfiguration<Mailbox>
{
    public void Configure(EntityTypeBuilder<Mailbox> builder)
    {
        builder.ToTable("com001_mailbox", CommunicationsModule.Schema);

        builder.HasKey(m => m.Id).HasName("pk_com001");

        builder.Property(m => m.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(m => m.UserId).HasColumnName("user_id").IsRequired();

        builder.Property(m => m.Provider)
               .HasColumnName("provider")
               .HasConversion(p => p.Value, v => MailProvider.FromValue(v))
               .HasMaxLength(20)
               .IsRequired();

        builder.Property(m => m.ExternalAccountId).HasColumnName("external_account_id").IsRequired();
        builder.Property(m => m.Address).HasColumnName("address").HasMaxLength(Mailbox.AddressMaxLength).IsRequired();
        builder.Property(m => m.DisplayName).HasColumnName("display_name").HasMaxLength(Mailbox.DisplayNameMaxLength);
        builder.Property(m => m.ImapHost).HasColumnName("imap_host").HasMaxLength(Mailbox.HostMaxLength);
        builder.Property(m => m.ImapPort).HasColumnName("imap_port");
        builder.Property(m => m.SmtpHost).HasColumnName("smtp_host").HasMaxLength(Mailbox.HostMaxLength);
        builder.Property(m => m.SmtpPort).HasColumnName("smtp_port");

        builder.Property(m => m.Status)
               .HasColumnName("status")
               .HasConversion(s => s.Value, v => MailboxStatus.FromValue(v))
               .HasMaxLength(20)
               .IsRequired();

        builder.Property(m => m.SyncCursor).HasColumnName("sync_cursor");
        builder.Property(m => m.BackfillCursor).HasColumnName("backfill_cursor");
        builder.Property(m => m.BackfillCompletedAt).HasColumnName("backfill_completed_at");
        builder.Property(m => m.MessageTotalEstimate).HasColumnName("message_total_estimate");
        builder.Property(m => m.LastSyncedAt).HasColumnName("last_synced_at");
        builder.Property(m => m.LastError).HasColumnName("last_error");
        builder.Property(m => m.ConsecutiveFailures).HasColumnName("consecutive_failures").IsRequired();

        builder.Property(m => m.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(m => m.CreatedBy).HasColumnName("created_by");
        builder.Property(m => m.UpdatedAt).HasColumnName("updated_at");
        builder.Property(m => m.UpdatedBy).HasColumnName("updated_by");
    }
}
