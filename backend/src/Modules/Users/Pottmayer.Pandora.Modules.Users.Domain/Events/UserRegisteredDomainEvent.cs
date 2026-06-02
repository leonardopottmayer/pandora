using Pottmayer.Tars.Core.Ddd;

namespace Pottmayer.Pandora.Modules.Users.Domain.Events;

public sealed record UserRegisteredDomainEvent(Guid UserId, string Email) : IDomainEvent;
