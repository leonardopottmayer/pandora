using Pottmayer.Tars.Core.Cqrs.Commands;

namespace Pottmayer.Pandora.Modules.Integrations.Application.Commands.SavePassword;

/// <summary>
/// Stores a username + password for a <c>password</c> provider (Titan, generic IMAP). Upserts on
/// (user, provider, username): the username is the account's identity, so saving the same mailbox again
/// replaces its password, while a second username is a second account. <paramref name="Label"/> is
/// optional (defaults to the username) and only applies when the account is created. Returns the
/// account's id.
/// </summary>
public sealed record SavePasswordInput(Guid UserId, string Provider, string Username, string Password, string? Label);

public sealed class SavePasswordCommand(SavePasswordInput input)
    : CommandBase<SavePasswordInput, Guid>(input);
