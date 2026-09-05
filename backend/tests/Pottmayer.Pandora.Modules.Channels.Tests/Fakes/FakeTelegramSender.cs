using Pottmayer.Pandora.Modules.Channels.Domain.Ports.Services;

namespace Pottmayer.Pandora.Modules.Channels.Tests.Fakes;

/// <summary>Records what the assistant-reply path asked to send; can be told to fail.</summary>
public sealed class FakeTelegramSender : ITelegramSender
{
    public List<(string Bot, string ChatId, string Text)> Sent { get; } = [];
    public Exception? Throw { get; set; }

    public Task SendAsync(string bot, string chatId, string text, CancellationToken ct = default)
    {
        if (Throw is not null)
            throw Throw;

        Sent.Add((bot, chatId, text));
        return Task.CompletedTask;
    }
}
