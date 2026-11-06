using Pottmayer.Tars.Core.Cqrs.Commands;
using Pottmayer.Tars.Security.Authorization.Abstractions;
using Pottmayer.Pandora.Modules.Integrations.Abstractions;

namespace Pottmayer.Pandora.Modules.Integrations.Application.Commands.ReplaceSecret;

/// <summary>
/// Replaces the API key or password of one existing account (the user rotated it). OAuth accounts are
/// renewed by reconnecting instead.
/// </summary>
public sealed record ReplaceSecretInput(Guid UserId, Guid AccountId, string Secret);

[RequiresPermission(IntegrationsPermissions.Accounts.Update)]
public sealed class ReplaceSecretCommand(ReplaceSecretInput input)
    : CommandBase<ReplaceSecretInput, bool>(input);
