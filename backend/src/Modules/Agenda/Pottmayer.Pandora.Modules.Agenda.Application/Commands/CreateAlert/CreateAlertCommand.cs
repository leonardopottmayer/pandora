using Pottmayer.Pandora.Modules.Agenda.Application.Dtos;
using Pottmayer.Tars.Core.Cqrs.Commands;

namespace Pottmayer.Pandora.Modules.Agenda.Application.Commands.CreateAlert;

/// <summary>
/// Adds an alert to a subject. <see cref="SubjectType"/> is the route segment ("task"); only tasks are
/// supported in this version. <see cref="Channels"/> null ⇒ resolve from the user's preference.
/// </summary>
public sealed record CreateAlertInput(
    Guid UserId, string SubjectType, Guid SubjectId, int OffsetMinutes, string[]? Channels);

public sealed class CreateAlertCommand(CreateAlertInput input)
    : CommandBase<CreateAlertInput, AlertDto>(input);
