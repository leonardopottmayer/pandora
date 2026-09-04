using Pottmayer.Pandora.Modules.Assistant.Application.Commands.SaveProfile;
using Pottmayer.Pandora.Modules.Assistant.Domain.Aggregates;
using Pottmayer.Pandora.Modules.Assistant.Domain.Ports.Repositories;
using Pottmayer.Pandora.Modules.Assistant.Domain.ValueObjects;
using Pottmayer.Pandora.Modules.Assistant.Tests.Fakes;
using Xunit;

namespace Pottmayer.Pandora.Modules.Assistant.Tests;

public sealed class SaveProfileCommandHandlerTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 3, 12, 0, 0, TimeSpan.Zero);
    private static readonly Guid User = Guid.NewGuid();

    private static (SaveProfileCommandHandler Handler, FakeAssistantProfileRepository Repo) Build(
        params AssistantProfile[] seed)
    {
        var repo = new FakeAssistantProfileRepository(seed);
        var ctx = new FakeDataContext().Register<IAssistantProfileRepository>(repo);
        var handler = new SaveProfileCommandHandler(new FakeUnitOfWorkFactory(ctx), new FixedTimeProvider(Now));
        return (handler, repo);
    }

    private static SaveProfileCommand Command(
        string model = "gemini-3.6-flash",
        string confirmationLevel = "balanced",
        string? localeOverride = null,
        bool isEnabled = true)
        => new(new SaveProfileInput(User, "gemini", model, isEnabled, localeOverride, confirmationLevel));

    [Fact]
    public async Task Creates_a_profile_when_none_exists()
    {
        var (handler, repo) = Build();

        var result = await handler.Handle(Command(localeOverride: "pt-BR"), CancellationToken.None);

        Assert.True(result.IsSuccess);
        var created = Assert.Single(repo.Added);
        Assert.Equal(User, created.UserId);
        Assert.Equal("gemini", created.ChatProvider);
        Assert.Equal("gemini-3.6-flash", created.ChatModel);
        Assert.True(created.IsEnabled);
        Assert.Equal("pt-BR", created.LocaleOverride);
        Assert.Same(ConfirmationLevel.Balanced, created.ConfirmationLevel);
        Assert.Empty(repo.Updated);
    }

    [Fact]
    public async Task Updates_the_existing_profile_instead_of_adding()
    {
        var existing = AssistantProfile.Create(
            User, "gemini", "old-model", isEnabled: false, null, ConfirmationLevel.Strict, new FixedTimeProvider(Now));
        var (handler, repo) = Build(existing);

        var result = await handler.Handle(Command(model: "gemini-3.6-pro", confirmationLevel: "trusting"), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Empty(repo.Added);
        var updated = Assert.Single(repo.Updated);
        Assert.Same(existing, updated);
        Assert.Equal("gemini-3.6-pro", updated.ChatModel);
        Assert.Same(ConfirmationLevel.Trusting, updated.ConfirmationLevel);
    }

    [Fact]
    public async Task Trims_the_model_and_normalizes_blank_locale_to_null()
    {
        var (handler, repo) = Build();

        var result = await handler.Handle(Command(model: "  gemini-3.6-flash  ", localeOverride: "   "), CancellationToken.None);

        Assert.True(result.IsSuccess);
        var created = Assert.Single(repo.Added);
        Assert.Equal("gemini-3.6-flash", created.ChatModel);
        Assert.Null(created.LocaleOverride);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Fails_when_model_is_blank(string model)
    {
        var (handler, repo) = Build();

        var result = await handler.Handle(Command(model: model), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("Assistant.ModelRequired", result.Errors[0].Code);
        Assert.Empty(repo.Added);
        Assert.Empty(repo.Updated);
    }

    [Fact]
    public async Task Fails_when_confirmation_level_is_unknown()
    {
        var (handler, repo) = Build();

        var result = await handler.Handle(Command(confirmationLevel: "aggressive"), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("Assistant.UnknownConfirmationLevel", result.Errors[0].Code);
        Assert.Empty(repo.Added);
    }
}
