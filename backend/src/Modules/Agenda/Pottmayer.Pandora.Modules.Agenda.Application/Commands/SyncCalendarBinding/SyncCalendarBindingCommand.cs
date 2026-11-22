using Pottmayer.Pandora.Modules.Agenda.Abstractions;
using Pottmayer.Pandora.Modules.Agenda.Application.Dtos;
using Pottmayer.Tars.Core.Cqrs.Commands;
using Pottmayer.Tars.Security.Authorization.Abstractions;

namespace Pottmayer.Pandora.Modules.Agenda.Application.Commands.SyncCalendarBinding;

public sealed record SyncCalendarBindingInput(Guid BindingId);

/// <summary>One sync cycle of a binding, run by the background job (no user behind it). Returns whether it ran.</summary>
public sealed class SyncCalendarBindingCommand(SyncCalendarBindingInput input)
    : CommandBase<SyncCalendarBindingInput, bool>(input);

public sealed record SyncCalendarBindingNowInput(Guid UserId, Guid BindingId);

/// <summary>"Sync now": resumes a stopped binding and runs a cycle at once. Returns the binding afterwards.</summary>
[RequiresPermission(AgendaPermissions.Calendars.Update)]
public sealed class SyncCalendarBindingNowCommand(SyncCalendarBindingNowInput input)
    : CommandBase<SyncCalendarBindingNowInput, CalendarBindingDto>(input);
