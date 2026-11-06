namespace Pottmayer.Pandora.Modules.Admin.Domain.Entities;

/// <summary>A per-user exception on one pattern. A deny beats every allow and every role.</summary>
public sealed class UserPermission
{
    public const string Allow = "allow";
    public const string Deny = "deny";

    public Guid UserId { get; private set; }
    public string Pattern { get; private set; } = default!;
    public string Effect { get; private set; } = default!;

    private UserPermission() { }
}
