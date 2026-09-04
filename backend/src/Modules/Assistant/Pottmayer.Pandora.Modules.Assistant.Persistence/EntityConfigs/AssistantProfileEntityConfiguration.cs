using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Pottmayer.Pandora.Modules.Assistant.Abstractions;
using Pottmayer.Pandora.Modules.Assistant.Domain.Aggregates;
using Pottmayer.Pandora.Modules.Assistant.Domain.ValueObjects;

namespace Pottmayer.Pandora.Modules.Assistant.Persistence.EntityConfigs;

internal sealed class AssistantProfileEntityConfiguration : IEntityTypeConfiguration<AssistantProfile>
{
    public void Configure(EntityTypeBuilder<AssistantProfile> builder)
    {
        builder.ToTable("ast001_assistant_profile", AssistantModule.Schema);

        builder.HasKey(p => p.Id).HasName("pk_ast001");

        builder.Property(p => p.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(p => p.UserId).HasColumnName("user_id").IsRequired();

        builder.Property(p => p.ChatProvider)
               .HasColumnName("chat_provider")
               .HasMaxLength(40)
               .IsRequired();

        builder.Property(p => p.ChatModel)
               .HasColumnName("chat_model")
               .HasMaxLength(100)
               .IsRequired();

        builder.Property(p => p.IsEnabled).HasColumnName("is_enabled").IsRequired();

        builder.Property(p => p.LocaleOverride)
               .HasColumnName("locale_override")
               .HasMaxLength(20);

        builder.Property(p => p.ConfirmationLevel)
               .HasColumnName("confirmation_level")
               .HasConversion(l => l.Value, v => ConfirmationLevel.FromValue(v))
               .HasMaxLength(20)
               .IsRequired();

        builder.HasIndex(p => p.UserId)
               .HasDatabaseName("uq_ast001_user")
               .IsUnique();

        builder.Property(p => p.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(p => p.CreatedBy).HasColumnName("created_by");
        builder.Property(p => p.UpdatedAt).HasColumnName("updated_at");
        builder.Property(p => p.UpdatedBy).HasColumnName("updated_by");
    }
}
