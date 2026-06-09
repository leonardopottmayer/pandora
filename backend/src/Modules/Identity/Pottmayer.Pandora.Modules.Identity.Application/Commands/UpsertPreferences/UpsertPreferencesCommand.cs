using Pottmayer.Pandora.Modules.Identity.Application.Dtos;
using Pottmayer.Tars.Core.Cqrs.Commands;

namespace Pottmayer.Pandora.Modules.Identity.Application.Commands.UpsertPreferences;

public sealed record UpsertPreferencesInput(Guid UserId, string Theme, string Language);

public sealed class UpsertPreferencesCommand(UpsertPreferencesInput input)
    : CommandBase<UpsertPreferencesInput, UserPreferencesDto>(input);
