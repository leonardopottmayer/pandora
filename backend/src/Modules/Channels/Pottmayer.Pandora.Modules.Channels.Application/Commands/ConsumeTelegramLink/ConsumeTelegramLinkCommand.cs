using Pottmayer.Tars.Core.Cqrs.Commands;

namespace Pottmayer.Pandora.Modules.Channels.Application.Commands.ConsumeTelegramLink;

/// <summary>
/// The inbound half of the handshake: a <c>/start &lt;token&gt;</c> arrived from a chat. The chat id
/// is trusted only because it came from Telegram carrying a code this system issued.
/// </summary>
public sealed record ConsumeTelegramLinkInput(string ChatId, string TokenPlaintext, string? Username, string? FirstName);

public sealed class ConsumeTelegramLinkCommand(ConsumeTelegramLinkInput input)
    : CommandBase<ConsumeTelegramLinkInput, Guid>(input);
