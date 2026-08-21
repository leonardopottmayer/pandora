using Pottmayer.Tars.Core.Cqrs.Commands;

namespace Pottmayer.Pandora.Modules.Agenda.Application.Commands.DeleteAlert;

public sealed record DeleteAlertInput(Guid UserId, Guid AlertId);

/// <summary>Removes an alert. Its dispatch ledger rows go with it (ON DELETE CASCADE).</summary>
public sealed class DeleteAlertCommand(DeleteAlertInput input)
    : CommandBase<DeleteAlertInput, bool>(input);
