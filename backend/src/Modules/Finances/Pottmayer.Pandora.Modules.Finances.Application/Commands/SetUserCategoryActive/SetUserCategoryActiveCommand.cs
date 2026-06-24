using Pottmayer.Pandora.Modules.Finances.Application.Dtos;
using Pottmayer.Tars.Core.Cqrs.Commands;

namespace Pottmayer.Pandora.Modules.Finances.Application.Commands.SetUserCategoryActive;

public sealed record SetUserCategoryActiveInput(Guid UserId, Guid CategoryId, bool Active);

/// <summary>
/// Activates or deactivates a category. An inactive category stays on past transactions but is no
/// longer offered for new ones. Setting the same state again is a no-op (no event is recorded).
/// </summary>
public sealed class SetUserCategoryActiveCommand(SetUserCategoryActiveInput input)
    : CommandBase<SetUserCategoryActiveInput, UserCategoryDto>(input);
