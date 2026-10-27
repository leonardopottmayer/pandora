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

    private static readonly ExternalAccountSummaryFixture Keys = new();

    private static (SaveProfileCommandHandler Handler, FakeAssistantProfileRepository Repo) Build(
        params AssistantProfile[] seed)
    {
        var repo = new FakeAssistantProfileRepository(seed);
        var ctx = new FakeDataContext().Register<IAssistantProfileRepository>(repo);
        var reader = new FakeExternalAccountReader(Keys.Mine, Keys.MineOtherProvider, Keys.SomeoneElses);
        var handler = new SaveProfileCommandHandler(new FakeUnitOfWorkFactory(ctx), reader, new FixedTimeProvider(Now));
        return (handler, repo);
    }

    private static SaveProfileCommand Command(
        string model = "gemini-3.6-flash",
        string confirmationLevel = "balanced",
        string? localeOverride = null,
        bool isEnabled = true,
        Guid? chatAccountId = null)
        => new(new SaveProfileInput(User, "gemini", chatAccountId, model, isEnabled, localeOverride, confirmationLevel));

    private sealed class ExternalAccountSummaryFixture
    {
        public readonly Pottmayer.Pandora.Modules.Integrations.Abstractions.Models.ExternalAccountSummary Mine =
            FakeExternalAccountReader.ApiKey(User);
        public readonly Pottmayer.Pandora.Modules.Integrations.Abstractions.Models.ExternalAccountSummary MineOtherProvider =
            FakeExternalAccountReader.ApiKey(User, "openai");
        public readonly Pottmayer.Pandora.Modules.Integrations.Abstractions.Models.ExternalAccountSummary SomeoneElses =
            FakeExternalAccountReader.ApiKey(Guid.NewGuid());
    }

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
            User, "gemini", null, "old-model", isEnabled: false, null, ConfirmationLevel.Strict, new FixedTimeProvider(Now));
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
    public async Task Stores_the_chosen_chat_account()
    {
        var (handler, repo) = Build();

        var result = await handler.Handle(Command(chatAccountId: Keys.Mine.Id), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(Keys.Mine.Id, Assert.Single(repo.Added).ChatAccountId);
    }

    [Fact]
    public async Task Refuses_an_account_of_another_user()
    {
        var (handler, repo) = Build();

        var result = await handler.Handle(Command(chatAccountId: Keys.SomeoneElses.Id), CancellationToken.None);

        Assert.Equal("Assistant.ChatAccountNotFound", result.Errors[0].Code);
        Assert.Empty(repo.Added);
    }

    [Fact]
    public async Task Refuses_an_account_of_another_provider()
    {
        var (handler, repo) = Build();

        var result = await handler.Handle(Command(chatAccountId: Keys.MineOtherProvider.Id), CancellationToken.None);

        Assert.Equal("Assistant.ChatAccountMismatch", result.Errors[0].Code);
        Assert.Empty(repo.Added);
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
