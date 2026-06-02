using Pottmayer.Pandora.Modules.Users.Application.Dtos;
using Pottmayer.Tars.Core.Cqrs.Commands;

namespace Pottmayer.Pandora.Modules.Users.Application.Commands.UpsertPreferences;

public sealed record UpsertPreferencesInput(Guid UserId, string Theme);

public sealed class UpsertPreferencesCommand(UpsertPreferencesInput input)
    : CommandBase<UpsertPreferencesInput, UserPreferencesDto>(input);
