namespace Pottmayer.Pandora.Modules.Admin.Domain.Entities;

/// <summary>
/// One change to flags, roles or grants, with the state before and after as JSON. <see cref="ActorId"/> is
/// <c>null</c> for changes the system makes itself (the master admin bootstrap, the default role).
/// </summary>
public sealed class AuditEntry
{
    public const string RoleAssigned = "role.assigned";
    public const string RoleRemoved = "role.removed";
    public const string RoleCreated = "role.created";
    public const string RoleUpdated = "role.updated";
    public const string RoleDeleted = "role.deleted";
    public const string ExceptionsChanged = "user.exceptions-changed";
    public const string UserDisabled = "user.disabled";
    public const string UserEnabled = "user.enabled";
    public const string UserApproved = "user.approved";
    public const string SessionsRevoked = "user.sessions-revoked";
    public const string FeatureChanged = "feature.changed";

    public const string UserTarget = "user";
    public const string RoleTarget = "role";
    public const string FeatureTarget = "feature";

    public Guid Id { get; private set; }
    public Guid? ActorId { get; private set; }
    public string Action { get; private set; } = default!;
    public string TargetType { get; private set; } = default!;
    public string TargetId { get; private set; } = default!;
    public string? Before { get; private set; }
    public string? After { get; private set; }
    public DateTimeOffset OccurredAt { get; private set; }

    private AuditEntry() { }

    public static AuditEntry Record(
        Guid? actorId, string action, string targetType, string targetId,
        string? before, string? after, TimeProvider timeProvider) => new()
    {
        Id = Guid.CreateVersion7(),
        ActorId = actorId,
        Action = action,
        TargetType = targetType,
        TargetId = targetId,
        Before = before,
        After = after,
        OccurredAt = timeProvider.GetUtcNow(),
    };
}
