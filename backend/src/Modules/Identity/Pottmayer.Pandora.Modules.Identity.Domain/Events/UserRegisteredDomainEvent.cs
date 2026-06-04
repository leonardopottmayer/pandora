using Pottmayer.Pandora.Shared.Domain.ValueObjects;
using Pottmayer.Tars.Core.Ddd;

namespace Pottmayer.Pandora.Modules.Identity.Domain.Events;

public sealed record UserRegisteredDomainEvent(Guid UserId, Email Email) : IDomainEvent;
