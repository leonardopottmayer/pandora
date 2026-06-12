using Pottmayer.Pandora.Modules.Finances.Application.Dtos;
using Pottmayer.Tars.Core.Cqrs.Commands;

namespace Pottmayer.Pandora.Modules.Finances.Application.Commands.CreateUserCategory;

public sealed record CreateUserCategoryInput(
    Guid UserId,
    string Name,
    string Nature,
    Guid? ParentCategoryId,
    string? Color,
    string? Icon,
    int DisplayOrder);

public sealed class CreateUserCategoryCommand(CreateUserCategoryInput input)
    : CommandBase<CreateUserCategoryInput, UserCategoryDto>(input);
