using Microsoft.Extensions.DependencyInjection;
using Pottmayer.Pandora.IntegrationTests.Support;
using Pottmayer.Pandora.Modules.Agenda.Application.Commands.CreateAlert;
using Pottmayer.Pandora.Modules.Agenda.Application.Commands.CreateCalendar;
using Pottmayer.Pandora.Modules.Agenda.Application.Commands.CreateEvent;
using Pottmayer.Pandora.Modules.Agenda.Application.Queries.GetAlerts;
using Pottmayer.Pandora.Modules.Agenda.Application.Queries.GetEvent;
using Pottmayer.Tars.Core.Cqrs.Commands;
using Pottmayer.Tars.Core.Cqrs.Queries;
using Pottmayer.Tars.Core.Mediator.Abstractions;
using Pottmayer.Tars.Core.Primitives.Outcomes;
using Xunit;

namespace Pottmayer.Pandora.IntegrationTests.Modules.Agenda;

/// <summary>
/// The two read queries the frontend needs: the event <em>row</em> (series, with its rrule) and the
/// alerts on a subject. Both mirror the shape the occurrence reads and the alert create already prove.
/// </summary>
[Collection("Integration")]
public sealed class AgendaReadQueryTests : IAsyncLifetime
{
    private readonly PandoraWebApplicationFactory _factory;
    private readonly Guid _userId = Guid.NewGuid();

    public AgendaReadQueryTests(PandoraWebApplicationFactory factory) => _factory = factory;

    public Task InitializeAsync() => _factory.ResetDatabaseAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    private static readonly DateTimeOffset Anchor = new(2026, 6, 1, 9, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task GetEvent_returns_the_series_row_including_its_rrule()
    {
        var calendarId = (await SendAsync(new CreateCalendarCommand(
            new CreateCalendarInput(_userId, "Work", null, true, "UTC")))).Value.Id;
        var ev = (await SendAsync(new CreateEventCommand(new CreateEventInput(
            _userId, calendarId, "Daily standup", null, null, null, Anchor, Anchor.AddHours(1),
            false, "UTC", "FREQ=DAILY", "Confirmed")))).Value;

        var result = await QueryAsync(new GetEventQuery(new GetEventInput(_userId, ev.Id)));

        Assert.True(result.IsSuccess);
        Assert.Equal(ev.Id, result.Value.Id);
        Assert.Equal("FREQ=DAILY", result.Value.Rrule);
        Assert.Equal(calendarId, result.Value.CalendarId);
    }

    [Fact]
    public async Task GetEvent_for_an_unknown_id_fails()
    {
        var result = await QueryAsync(new GetEventQuery(new GetEventInput(_userId, Guid.NewGuid())));
        Assert.True(result.IsFailure);
    }

    [Fact]
    public async Task GetAlerts_lists_the_alerts_created_on_an_event()
    {
        var calendarId = (await SendAsync(new CreateCalendarCommand(
            new CreateCalendarInput(_userId, "Work", null, true, "UTC")))).Value.Id;
        var ev = (await SendAsync(new CreateEventCommand(new CreateEventInput(
            _userId, calendarId, "Review", null, null, null, Anchor, Anchor.AddHours(1),
            false, "UTC", null, "Confirmed")))).Value;

        await SendAsync(new CreateAlertCommand(new CreateAlertInput(_userId, "event", ev.Id, -15, null)));
        await SendAsync(new CreateAlertCommand(new CreateAlertInput(_userId, "event", ev.Id, -60, null)));

        var result = await QueryAsync(new GetAlertsQuery(new GetAlertsInput(_userId, "event", ev.Id)));

        Assert.True(result.IsSuccess);
        Assert.Equal(2, result.Value.Count);
        Assert.Contains(result.Value, a => a.OffsetMinutes == -15);
        Assert.Contains(result.Value, a => a.OffsetMinutes == -60);
    }

    [Fact]
    public async Task GetAlerts_rejects_an_unsupported_subject_type()
    {
        var result = await QueryAsync(new GetAlertsQuery(new GetAlertsInput(_userId, "reminder", Guid.NewGuid())));
        Assert.True(result.IsFailure);
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
