using Microsoft.Extensions.DependencyInjection;
using Pottmayer.Pandora.IntegrationTests.Support;
using Pottmayer.Pandora.Modules.Agenda.Application.Commands.SavePreferences;
using Pottmayer.Pandora.Modules.Agenda.Application.Queries.GetPreferences;
using Pottmayer.Tars.Core.Mediator.Abstractions;
using Xunit;

namespace Pottmayer.Pandora.IntegrationTests.Modules.Agenda;

[Collection("Integration")]
public sealed class AgendaPreferencesTests : IAsyncLifetime
{
    private readonly PandoraWebApplicationFactory _factory;
    private readonly Guid _userId = Guid.NewGuid();

    public AgendaPreferencesTests(PandoraWebApplicationFactory factory) => _factory = factory;

    public Task InitializeAsync() => _factory.ResetDatabaseAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task Never_saved_answers_the_default()
    {
        Assert.Equal(-15, await ReadAsync(_userId));
    }

    [Fact]
    public async Task Saving_creates_then_updates_and_only_for_that_user()
    {
        await SaveAsync(_userId, -30);
        Assert.Equal(-30, await ReadAsync(_userId));

        await SaveAsync(_userId, 0);
        Assert.Equal(0, await ReadAsync(_userId));

        Assert.Equal(-15, await ReadAsync(Guid.NewGuid()));
    }

    private async Task SaveAsync(Guid userId, int offset)
    {
        using var scope = _factory.Services.CreateScope();
        var result = await scope.ServiceProvider.GetRequiredService<ISender>().Send(new SavePreferencesCommand(new SavePreferencesInput(userId, offset)));
        Assert.True(result.IsSuccess);
    }

    private async Task<int> ReadAsync(Guid userId)
    {
        using var scope = _factory.Services.CreateScope();
        var result = await scope.ServiceProvider.GetRequiredService<ISender>().Send(new GetPreferencesQuery(new GetPreferencesInput(userId)));
        return result.Value.DefaultAlertOffsetMinutes;
    }
}
