using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Pottmayer.Pandora.Modules.Assistant.Abstractions;
using Pottmayer.Pandora.Modules.Assistant.Domain.Aggregates;

namespace Pottmayer.Pandora.Modules.Assistant.Persistence.EntityConfigs;

internal sealed class ConversationEntityConfiguration : IEntityTypeConfiguration<Conversation>
{
    public void Configure(EntityTypeBuilder<Conversation> builder)
    {
        builder.ToTable("ast002_conversation", AssistantModule.Schema);

        builder.HasKey(c => c.Id).HasName("pk_ast002");

        builder.Property(c => c.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(c => c.UserId).HasColumnName("user_id").IsRequired();
        builder.Property(c => c.StartedAt).HasColumnName("started_at").IsRequired();
        builder.Property(c => c.LastActivityAt).HasColumnName("last_activity_at").IsRequired();

        builder.HasIndex(c => new { c.UserId, c.LastActivityAt })
               .HasDatabaseName("ix_ast002_user_activity");
    }
}
