using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Pottmayer.Pandora.Modules.Communications.Abstractions;
using Pottmayer.Pandora.Modules.Communications.Domain.Entities;
using Pottmayer.Pandora.Modules.Communications.Domain.ValueObjects;

namespace Pottmayer.Pandora.Modules.Communications.Persistence.EntityConfigs;

internal sealed class MailRuleEntityConfiguration : IEntityTypeConfiguration<MailRule>
{
    public void Configure(EntityTypeBuilder<MailRule> builder)
    {
        builder.ToTable("com006_mail_rule", CommunicationsModule.Schema);

        builder.HasKey(r => r.Id).HasName("pk_com006");

        builder.Property(r => r.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(r => r.MailboxId).HasColumnName("mailbox_id").IsRequired();
        builder.Property(r => r.Name).HasColumnName("name").HasMaxLength(MailRule.NameMaxLength).IsRequired();
        builder.Property(r => r.Position).HasColumnName("position").IsRequired();
        builder.ComplexCollection(r => r.Conditions, b => b.ToJson("conditions"));
        builder.ComplexCollection(r => r.Actions, b => b.ToJson("actions"));

        builder.Property(r => r.Execution)
               .HasColumnName("execution")
               .HasConversion(e => e.Value, v => RuleExecution.FromValue(v))
               .HasMaxLength(20)
               .IsRequired();

        builder.Property(r => r.ProviderFilterId).HasColumnName("provider_filter_id");
        builder.Property(r => r.ProviderFilterRaw).HasColumnName("provider_filter_raw").HasColumnType("jsonb");
        builder.Property(r => r.IsEnabled).HasColumnName("is_enabled").IsRequired();

        builder.Property(r => r.Status)
               .HasColumnName("status")
               .HasConversion(s => s.Value, v => RuleStatus.FromValue(v))
               .HasMaxLength(20)
               .IsRequired();

        builder.Property(r => r.LastError).HasColumnName("last_error");

        builder.Ignore(r => r.IsReadOnly);
        builder.Ignore(r => r.RunsInPandora);
    }
}
