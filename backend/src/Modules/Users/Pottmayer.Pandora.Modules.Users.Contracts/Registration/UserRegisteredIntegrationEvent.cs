namespace Pottmayer.Pandora.Modules.Users.Contracts.Registration;

public sealed record UserRegisteredIntegrationEvent(Guid UserId, string Email);
