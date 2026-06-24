using Pottmayer.Pandora.Modules.Finances.Application.Dtos;
using Pottmayer.Tars.Core.Cqrs.Commands;

namespace Pottmayer.Pandora.Modules.Finances.Application.Commands.UpdateUserCategory;

public sealed record UpdateUserCategoryInput(
    Guid UserId,
    Guid CategoryId,
    string Name,
    string? Color,
    string? Icon,
    int DisplayOrder);

/// <summary>Edits a category's display fields. Nature and parent are fixed once created.</summary>
public sealed class UpdateUserCategoryCommand(UpdateUserCategoryInput input)
    : CommandBase<UpdateUserCategoryInput, UserCategoryDto>(input);
