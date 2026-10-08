using Pottmayer.Pandora.Shared.Domain;

namespace Pottmayer.Pandora.Modules.Files.Domain.Entities;

/// <summary>The account switch: "I use Files". Off by default; agents get no roots while it is off.</summary>
public sealed class Preferences : IAuditable
{
    public Guid UserId { get; private set; }
    public bool IsEnabled { get; private set; }

    public Guid? CreatedBy { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public Guid? UpdatedBy { get; set; }
    public DateTimeOffset? UpdatedAt { get; set; }

    private Preferences() { }

    public static Preferences Create(Guid userId, bool isEnabled) => new() { UserId = userId, IsEnabled = isEnabled };

    public void SetEnabled(bool isEnabled) => IsEnabled = isEnabled;
}
