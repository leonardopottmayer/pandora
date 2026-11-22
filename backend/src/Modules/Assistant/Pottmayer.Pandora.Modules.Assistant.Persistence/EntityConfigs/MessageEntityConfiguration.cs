using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Pottmayer.Pandora.Modules.Assistant.Abstractions;
using Pottmayer.Pandora.Modules.Assistant.Domain.Aggregates;
using Pottmayer.Pandora.Modules.Assistant.Domain.ValueObjects;

namespace Pottmayer.Pandora.Modules.Assistant.Persistence.EntityConfigs;

internal sealed class MessageEntityConfiguration : IEntityTypeConfiguration<Message>
{
    public void Configure(EntityTypeBuilder<Message> builder)
    {
        builder.ToTable("ast003_message", AssistantModule.Schema);

        builder.HasKey(m => m.Id).HasName("pk_ast003");

        builder.Property(m => m.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(m => m.ConversationId).HasColumnName("conversation_id").IsRequired();

        builder.Property(m => m.Author)
               .HasColumnName("author")
               .HasConversion(a => a.ToString().ToLowerInvariant(), v => Enum.Parse<MessageAuthor>(v, ignoreCase: true))
               .HasMaxLength(20)
               .IsRequired();

        builder.Property(m => m.Content).HasColumnName("content").IsRequired();
        builder.Property(m => m.CreatedAt).HasColumnName("created_at").IsRequired();

        builder.HasIndex(m => new { m.ConversationId, m.CreatedAt })
               .HasDatabaseName("ix_ast003_conversation_created");
    }
}
