using Pottmayer.Tars.Communication.Telegram.Abstractions;

namespace Pottmayer.Pandora.Modules.Channels.Tests.Fakes;

/// <summary>
/// Hands back a single <see cref="ITelegramClient"/> for any bot name, so a test can inject the factory the
/// production consumers expect while still asserting against one fake client.
/// </summary>
public sealed class FakeTelegramClientFactory(ITelegramClient client) : ITelegramClientFactory
{
    public ITelegramClient GetClient(string name = ITelegramClientFactory.DefaultBotName) => client;
}
