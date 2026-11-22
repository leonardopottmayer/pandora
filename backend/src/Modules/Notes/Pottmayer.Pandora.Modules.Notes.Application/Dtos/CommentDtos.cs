using Pottmayer.Pandora.Modules.Notes.Domain.Aggregates;

namespace Pottmayer.Pandora.Modules.Notes.Application.Dtos;

/// <summary>A comment; a thread is the one with no <see cref="ThreadId"/>, its replies point at it.</summary>
public sealed record CommentDto(
    Guid Id, Guid PageId, string BlockId, Guid? ThreadId, string? Quote, string Body,
    DateTimeOffset? ResolvedAt, DateTimeOffset CreatedAt, DateTimeOffset? EditedAt)
{
    public static CommentDto From(PageComment c) =>
        new(c.Id, c.PageId, c.BlockId, c.ThreadId, c.Quote, c.Body, c.ResolvedAt, c.CreatedAt, c.EditedAt);
}

/// <summary>When a block was written and last changed, and by what (<c>edit</c>, <c>create</c>, <c>assistant</c>, <c>restore</c>).</summary>
public sealed record BlockStampDto(string BlockId, DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt, string UpdatedSource)
{
    public static BlockStampDto From(BlockStamp s) => new(s.BlockId, s.CreatedAt, s.UpdatedAt, s.UpdatedSource);
}
