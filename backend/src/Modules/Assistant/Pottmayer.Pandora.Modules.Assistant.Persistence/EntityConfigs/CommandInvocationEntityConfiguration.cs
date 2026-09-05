using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Pottmayer.Pandora.Modules.Assistant.Abstractions;
using Pottmayer.Pandora.Modules.Assistant.Domain.Aggregates;
using Pottmayer.Pandora.Modules.Assistant.Domain.ValueObjects;

namespace Pottmayer.Pandora.Modules.Assistant.Persistence.EntityConfigs;

internal sealed class CommandInvocationEntityConfiguration : IEntityTypeConfiguration<CommandInvocation>
{
    public void Configure(EntityTypeBuilder<CommandInvocation> builder)
    {
        builder.ToTable("ast004_command_invocation", AssistantModule.Schema);

        builder.HasKey(i => i.Id).HasName("pk_ast004");

        builder.Property(i => i.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(i => i.UserId).HasColumnName("user_id").IsRequired();
        builder.Property(i => i.ConversationId).HasColumnName("conversation_id").IsRequired();

        builder.Property(i => i.Utterance).HasColumnName("utterance").IsRequired();

        builder.Property(i => i.CommandName).HasColumnName("command_name").HasMaxLength(100);

        builder.Property(i => i.ArgumentsJson).HasColumnName("arguments").HasColumnType("jsonb");

        builder.Property(i => i.Status)
               .HasColumnName("status")
               .HasConversion(s => s.Value, v => InvocationStatus.FromValue(v))
               .HasMaxLength(20)
               .IsRequired();

        builder.Property(i => i.Result).HasColumnName("result");
        builder.Property(i => i.Error).HasColumnName("error");

        builder.Property(i => i.Provider).HasColumnName("provider").HasMaxLength(40).IsRequired();
        builder.Property(i => i.Model).HasColumnName("model").HasMaxLength(100).IsRequired();
        builder.Property(i => i.LatencyMs).HasColumnName("latency_ms").IsRequired();
        builder.Property(i => i.PromptTokens).HasColumnName("prompt_tokens").IsRequired();
        builder.Property(i => i.CompletionTokens).HasColumnName("completion_tokens").IsRequired();

        builder.Property(i => i.ExpiresAt).HasColumnName("expires_at");
        builder.Property(i => i.CreatedAt).HasColumnName("created_at").IsRequired();

        builder.HasIndex(i => new { i.UserId, i.CreatedAt })
               .HasDatabaseName("ix_ast004_user_created");
    }
}
