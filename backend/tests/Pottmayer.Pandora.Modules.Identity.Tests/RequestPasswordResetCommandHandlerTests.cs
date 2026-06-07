using Microsoft.Extensions.Options;
using Pottmayer.Pandora.Modules.Identity.Application.Commands.PasswordReset;
using Pottmayer.Pandora.Modules.Identity.Application.Options;
using Pottmayer.Pandora.Modules.Identity.Contracts.IntegrationEvents;
using Pottmayer.Pandora.Modules.Identity.Domain.Aggregates;
using Pottmayer.Pandora.Modules.Identity.Domain.Ports.Repositories;
using Pottmayer.Pandora.Modules.Identity.Tests.Fakes;
using Xunit;

namespace Pottmayer.Pandora.Modules.Identity.Tests;

public sealed class RequestPasswordResetCommandHandlerTests
{
    private readonly FakePasswordHasher _hasher = new();
    private readonly FakeIntegrationEventBus _eventBus = new();

    private (RequestPasswordResetCommandHandler Handler, FakePasswordResetTokenRepository Tokens) Build(params User[] users)
    {
        var tokens  = new FakePasswordResetTokenRepository();
        var ctx     = new FakeDataContext()
            .Register<IUserRepository>(new FakeUserRepository(users))
            .Register<IPasswordResetTokenRepository>(tokens);
        var options = Options.Create(new PasswordResetOptions());
        var handler = new RequestPasswordResetCommandHandler(
            new FakeUnitOfWorkFactory(ctx), _eventBus, options, TimeProvider.System);
        return (handler, tokens);
    }

    private static RequestPasswordResetCommand Command(string email = "alice@example.com")
        => new(new RequestPasswordResetInput(email));

    [Fact]
    public async Task Issues_token_and_publishes_event_for_active_user()
    {
        var (handler, tokens) = Build(TestUsers.Active(_hasher));

        var result = await handler.Handle(Command(), CancellationToken.None);

        Assert.True(result.IsSuccess);
        var stored = Assert.Single(tokens.Tokens);
        var published = Assert.Single(_eventBus.Published);
        var requested = Assert.IsType<PasswordResetRequested>(published);
        Assert.Equal(PasswordResetTokens.Hash(requested.Token), stored.TokenHash);
    }

    [Fact]
    public async Task Succeeds_silently_for_unknown_email()
    {
        var (handler, tokens) = Build();

        var result = await handler.Handle(Command("ghost@example.com"), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Empty(tokens.Tokens);
        Assert.Empty(_eventBus.Published);
    }

    [Fact]
    public async Task Does_not_issue_token_for_pending_user()
    {
        var (handler, tokens) = Build(TestUsers.Unconfirmed(_hasher));

        var result = await handler.Handle(Command(), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Empty(tokens.Tokens);
        Assert.Empty(_eventBus.Published);
    }
}
