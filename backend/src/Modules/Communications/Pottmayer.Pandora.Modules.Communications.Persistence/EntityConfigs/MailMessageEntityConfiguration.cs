using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Pottmayer.Pandora.Modules.Communications.Abstractions;
using Pottmayer.Pandora.Modules.Communications.Domain.Entities;

namespace Pottmayer.Pandora.Modules.Communications.Persistence.EntityConfigs;

internal sealed class MailMessageEntityConfiguration : IEntityTypeConfiguration<MailMessage>
{
    public void Configure(EntityTypeBuilder<MailMessage> builder)
    {
        builder.ToTable("com003_mail_message", CommunicationsModule.Schema);

        builder.HasKey(m => m.Id).HasName("pk_com003");

        builder.Property(m => m.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(m => m.MailboxId).HasColumnName("mailbox_id").IsRequired();
        builder.Property(m => m.ProviderMessageId).HasColumnName("provider_message_id").IsRequired();
        builder.Property(m => m.ThreadKey).HasColumnName("thread_key").IsRequired();
        builder.Property(m => m.Rfc822MessageId).HasColumnName("rfc822_message_id");
        builder.Property(m => m.FromName).HasColumnName("from_name");
        builder.Property(m => m.FromAddress).HasColumnName("from_address").HasMaxLength(MailMessage.AddressMaxLength);
        builder.ComplexCollection(m => m.To, b => b.ToJson("to_recipients"));
        builder.ComplexCollection(m => m.Cc, b => b.ToJson("cc_recipients"));
        builder.Property(m => m.Subject).HasColumnName("subject").IsRequired();
        builder.Property(m => m.Snippet).HasColumnName("snippet").IsRequired();
        builder.Property(m => m.SentAt).HasColumnName("sent_at").IsRequired();
        builder.Property(m => m.IsUnread).HasColumnName("is_unread").IsRequired();
        builder.Property(m => m.IsStarred).HasColumnName("is_starred").IsRequired();
        builder.Property(m => m.HasAttachments).HasColumnName("has_attachments").IsRequired();
        builder.Property(m => m.SizeBytes).HasColumnName("size_bytes").IsRequired();

        builder.HasMany(m => m.Labels)
               .WithOne()
               .HasForeignKey(l => l.MessageId)
               .OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(m => m.Labels).UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}

internal sealed class MailMessageLabelEntityConfiguration : IEntityTypeConfiguration<MailMessageLabel>
{
    public void Configure(EntityTypeBuilder<MailMessageLabel> builder)
    {
        builder.ToTable("com004_mail_message_label", CommunicationsModule.Schema);

        builder.HasKey(l => new { l.MessageId, l.LabelId }).HasName("pk_com004");

        builder.Property(l => l.MessageId).HasColumnName("message_id");
        builder.Property(l => l.LabelId).HasColumnName("label_id");
    }
}
