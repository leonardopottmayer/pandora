using Pottmayer.Pandora.Modules.Identity.Domain.ValueObjects;
using Pottmayer.Pandora.Shared.Domain;
using Pottmayer.Tars.Core.Ddd;

namespace Pottmayer.Pandora.Modules.Identity.Domain.Entities;

public sealed class UserPreferences : Entity<Guid>, IAuditable
{
    public Guid UserId { get; private set; }
    public AppTheme Theme { get; private set; } = null!;

    public Guid? CreatedBy { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public Guid? UpdatedBy { get; set; }
    public DateTimeOffset? UpdatedAt { get; set; }

    private UserPreferences() { }

    internal static UserPreferences Create(AppTheme theme) =>
        new() { Id = Guid.CreateVersion7(), Theme = theme };

    public void Update(AppTheme theme) => Theme = theme;
}
