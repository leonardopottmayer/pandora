using Pottmayer.Pandora.Modules.Finances.Application.Dtos;
using Pottmayer.Tars.Core.Cqrs.Commands;

namespace Pottmayer.Pandora.Modules.Finances.Application.Commands.SetUserCategoryActive;

public sealed record SetUserCategoryActiveInput(Guid UserId, Guid CategoryId, bool Active);

public sealed class SetUserCategoryActiveCommand(SetUserCategoryActiveInput input)
    : CommandBase<SetUserCategoryActiveInput, UserCategoryDto>(input);
