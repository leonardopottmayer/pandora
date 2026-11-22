using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Pottmayer.Pandora.Modules.Communications.Abstractions;
using Pottmayer.Pandora.Modules.Communications.Domain.Entities;

namespace Pottmayer.Pandora.Modules.Communications.Persistence.EntityConfigs;

internal sealed class MailContentEntityConfiguration : IEntityTypeConfiguration<MailContent>
{
    public void Configure(EntityTypeBuilder<MailContent> builder)
    {
        builder.ToTable("com005_mail_content", CommunicationsModule.Schema);

        builder.HasKey(c => c.MessageId).HasName("pk_com005");

        builder.Property(c => c.MessageId).HasColumnName("message_id").ValueGeneratedNever();
        builder.Property(c => c.TextBody).HasColumnName("text_body");
        builder.Property(c => c.HtmlBody).HasColumnName("html_body");
        builder.ComplexCollection(c => c.Attachments, b => b.ToJson("attachments"));
        builder.Property(c => c.FetchedAt).HasColumnName("fetched_at").IsRequired();
        builder.Property(c => c.LastAccessedAt).HasColumnName("last_accessed_at").IsRequired();
    }
}
