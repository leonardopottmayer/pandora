using Pottmayer.Tars.Core.Cqrs.Commands;
using Pottmayer.Tars.Security.Authorization.Abstractions;
using Pottmayer.Pandora.Modules.Agenda.Abstractions;

namespace Pottmayer.Pandora.Modules.Agenda.Application.Commands.SnoozeOccurrence;

public sealed record SnoozeOccurrenceInput(Guid UserId, Guid ReminderId, DateTimeOffset OccurrenceStartsAt, DateTimeOffset SnoozedUntil);

/// <summary>Snoozes one occurrence of a recurring reminder; the sweep re-fires it once when the snooze passes.</summary>
[RequiresPermission(AgendaPermissions.Events.Update)]
public sealed class SnoozeOccurrenceCommand(SnoozeOccurrenceInput input)
    : CommandBase<SnoozeOccurrenceInput, bool>(input);
