using Pottmayer.Pandora.Modules.Communications.Application.Dtos;
using Pottmayer.Tars.Core.Cqrs.Commands;

namespace Pottmayer.Pandora.Modules.Communications.Application.Commands.OpenMessage;

public sealed record OpenMessageInput(Guid UserId, Guid MessageId);

/// <summary>
/// The message with its body. The first open fetches the raw message from the provider and caches the
/// body (and fills the IMAP snippet); later opens read the cache and restart its retention clock.
/// The message is not marked as read.
/// </summary>
public sealed class OpenMessageCommand(OpenMessageInput input)
    : CommandBase<OpenMessageInput, MessageDto>(input);
