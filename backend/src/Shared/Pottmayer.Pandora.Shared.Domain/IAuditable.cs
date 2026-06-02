namespace Pottmayer.Pandora.Shared.Domain;

public interface IAuditable
{
    Guid? CreatedBy { get; }
    DateTimeOffset CreatedAt { get; }
    Guid? UpdatedBy { get; }
    DateTimeOffset? UpdatedAt { get; }
}
