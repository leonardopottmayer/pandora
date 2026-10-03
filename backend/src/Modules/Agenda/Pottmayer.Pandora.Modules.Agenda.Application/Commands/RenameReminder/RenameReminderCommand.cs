using Pottmayer.Tars.Core.Cqrs.Commands;

namespace Pottmayer.Pandora.Modules.Agenda.Application.Commands.RenameReminder;

public sealed record RenameReminderInput(Guid UserId, Guid ReminderId, string Title);

/// <summary>Changes a reminder's title; its time, recurrence and status are untouched.</summary>
public sealed class RenameReminderCommand(RenameReminderInput input)
    : CommandBase<RenameReminderInput, bool>(input);
