using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Pottmayer.Pandora.Modules.Communications.Abstractions;
using Pottmayer.Pandora.Modules.Communications.Domain.Entities;
using Pottmayer.Pandora.Modules.Communications.Domain.ValueObjects;

namespace Pottmayer.Pandora.Modules.Communications.Persistence.EntityConfigs;

internal sealed class MailLabelEntityConfiguration : IEntityTypeConfiguration<MailLabel>
{
    public void Configure(EntityTypeBuilder<MailLabel> builder)
    {
        builder.ToTable("com002_mail_label", CommunicationsModule.Schema);

        builder.HasKey(l => l.Id).HasName("pk_com002");

        builder.Property(l => l.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(l => l.MailboxId).HasColumnName("mailbox_id").IsRequired();
        builder.Property(l => l.ProviderLabelId).HasColumnName("provider_label_id").IsRequired();
        builder.Property(l => l.Name).HasColumnName("name").IsRequired();

        builder.Property(l => l.Kind)
               .HasColumnName("kind")
               .HasConversion(k => k.Value, v => LabelKind.FromValue(v))
               .HasMaxLength(20)
               .IsRequired();

        builder.Property(l => l.Role)
               .HasColumnName("role")
               .HasConversion(r => r!.Value, v => LabelRole.FromValue(v))
               .HasMaxLength(20);

        builder.Property(l => l.IsExclusive).HasColumnName("is_exclusive").IsRequired();
    }
}
