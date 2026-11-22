namespace Pottmayer.Pandora.Modules.Admin.Application.Dtos;

/// <summary>One change made in the panel or by the system (no actor).</summary>
/// <param name="TargetName">The user's or role's name when it still exists.</param>
/// <param name="Before">The state before, as JSON.</param>
/// <param name="After">The state after, as JSON.</param>
public sealed record AuditEntryDto(
    Guid Id,
    Guid? ActorId,
    string? ActorName,
    string Action,
    string TargetType,
    string TargetId,
    string? TargetName,
    string? Before,
    string? After,
    DateTimeOffset OccurredAt);
