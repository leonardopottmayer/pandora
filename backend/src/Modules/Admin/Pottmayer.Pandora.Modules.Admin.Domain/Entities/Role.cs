using Pottmayer.Pandora.Shared.Domain;

namespace Pottmayer.Pandora.Modules.Admin.Domain.Entities;

/// <summary>A named set of permission patterns. The default role is given to every account on activation.</summary>
public sealed class Role : IAuditable
{
    public Guid Id { get; private set; }
    public string Name { get; private set; } = default!;
    public string? Description { get; private set; }
    public bool IsSystem { get; private set; }
    public bool IsDefault { get; private set; }

    public Guid? CreatedBy { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public Guid? UpdatedBy { get; set; }
    public DateTimeOffset? UpdatedAt { get; set; }

    private Role() { }
}
