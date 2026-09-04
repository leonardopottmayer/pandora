using Pottmayer.Pandora.Modules.Assistant.Domain.Aggregates;
using Pottmayer.Pandora.Modules.Assistant.Domain.ValueObjects;
using Pottmayer.Pandora.Modules.Assistant.Tests.Fakes;
using Xunit;

namespace Pottmayer.Pandora.Modules.Assistant.Tests;

public sealed class AssistantProfileTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 3, 12, 0, 0, TimeSpan.Zero);
    private static readonly TimeProvider Clock = new FixedTimeProvider(Now);

    [Fact]
    public void Create_sets_every_field_and_stamps_created_at()
    {
        var userId = Guid.NewGuid();

        var profile = AssistantProfile.Create(
            userId, "gemini", "gemini-3.6-flash", isEnabled: true, "pt-BR", ConfirmationLevel.Trusting, Clock);

        Assert.NotEqual(Guid.Empty, profile.Id);
        Assert.Equal(userId, profile.UserId);
        Assert.Equal("gemini", profile.ChatProvider);
        Assert.Equal("gemini-3.6-flash", profile.ChatModel);
        Assert.True(profile.IsEnabled);
        Assert.Equal("pt-BR", profile.LocaleOverride);
        Assert.Same(ConfirmationLevel.Trusting, profile.ConfirmationLevel);
        Assert.Equal(Now, profile.CreatedAt);
    }

    [Fact]
    public void Update_replaces_mutable_fields_but_keeps_identity_and_created_at()
    {
        var userId = Guid.NewGuid();
        var profile = AssistantProfile.Create(
            userId, "gemini", "gemini-3.6-flash", isEnabled: false, null, ConfirmationLevel.Balanced, Clock);
        var originalId = profile.Id;

        profile.Update("gemini", "gemini-3.6-pro", isEnabled: true, "en", ConfirmationLevel.Strict);

        Assert.Equal(originalId, profile.Id);
        Assert.Equal(userId, profile.UserId);
        Assert.Equal(Now, profile.CreatedAt);
        Assert.Equal("gemini-3.6-pro", profile.ChatModel);
        Assert.True(profile.IsEnabled);
        Assert.Equal("en", profile.LocaleOverride);
        Assert.Same(ConfirmationLevel.Strict, profile.ConfirmationLevel);
    }
}
