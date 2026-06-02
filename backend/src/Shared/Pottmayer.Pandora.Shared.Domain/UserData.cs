using Pottmayer.Tars.UserContext.Abstractions;

namespace Pottmayer.Pandora.Shared.Domain;

public sealed class UserData
{
    [Claim("Id")]
    public Guid Id { get; set; }
}
