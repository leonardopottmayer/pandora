using System.Linq;
using Microsoft.Extensions.DependencyInjection;
using Pottmayer.Pandora.IntegrationTests.Support;
using Pottmayer.Pandora.Modules.Agenda.Application.Commands.CreateCalendar;
using Pottmayer.Pandora.Modules.Agenda.Application.Commands.UpdateCalendar;
using Pottmayer.Pandora.Modules.Agenda.Application.Queries.GetCalendars;
using Pottmayer.Tars.Core.Cqrs.Commands;
using Pottmayer.Tars.Core.Cqrs.Queries;
using Pottmayer.Tars.Core.Mediator.Abstractions;
using Pottmayer.Tars.Core.Primitives.Outcomes;
using Xunit;

namespace Pottmayer.Pandora.IntegrationTests.Modules.Agenda;

/// <summary>
/// Promoting a calendar to default must demote the previous one, so the partial unique index
/// (one default per user) always holds. This is what makes the Agenda settings "default calendar"
/// picker work.
/// </summary>
[Collection("Integration")]
public sealed class CalendarDefaultTests : IAsyncLifetime
{
    private readonly PandoraWebApplicationFactory _factory;
    private readonly Guid _userId = Guid.NewGuid();

    public CalendarDefaultTests(PandoraWebApplicationFactory factory) => _factory = factory;

    public Task InitializeAsync() => _factory.ResetDatabaseAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task Promoting_a_calendar_to_default_demotes_the_previous_default()
    {
        var work = (await SendAsync(new CreateCalendarCommand(
            new CreateCalendarInput(_userId, "Work", null, true, "UTC")))).Value;
        var personal = (await SendAsync(new CreateCalendarCommand(
            new CreateCalendarInput(_userId, "Personal", null, false, "UTC")))).Value;

        var updated = await SendAsync(new UpdateCalendarCommand(new UpdateCalendarInput(
            _userId, personal.Id, null, null, null, null, IsDefault: true, Archive: false)));
        Assert.True(updated.IsSuccess);

        var calendars = (await QueryAsync(new GetCalendarsQuery(new GetCalendarsInput(_userId)))).Value;

        Assert.Equal(1, calendars.Count(c => c.IsDefault));
        Assert.True(calendars.Single(c => c.Id == personal.Id).IsDefault);
        Assert.False(calendars.Single(c => c.Id == work.Id).IsDefault);
    }

    // ── helpers ──

    private async Task<Result<TResult>> SendAsync<TResult>(ICommand<TResult> command)
        where TResult : notnull
    {
        using var scope = _factory.Services.CreateScope();
        var sender = scope.ServiceProvider.GetRequiredService<ISender>();
        return await sender.Send(command, CancellationToken.None);
    }

    private async Task<Result<TResult>> QueryAsync<TResult>(IQuery<TResult> query)
        where TResult : notnull
    {
        using var scope = _factory.Services.CreateScope();
        var sender = scope.ServiceProvider.GetRequiredService<ISender>();
        return await sender.Send(query, CancellationToken.None);
    }
}
