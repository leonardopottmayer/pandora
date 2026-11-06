using Pottmayer.Pandora.Modules.Finances.Application.Dtos;
using Pottmayer.Tars.Core.Cqrs.Commands;
using Pottmayer.Tars.Security.Authorization.Abstractions;
using Pottmayer.Pandora.Modules.Finances.Abstractions;

namespace Pottmayer.Pandora.Modules.Finances.Application.Commands.CreateUserCategory;

public sealed record CreateUserCategoryInput(
    Guid UserId,
    string Name,
    string Nature,
    Guid? ParentCategoryId,
    string? Color,
    string? Icon,
    int DisplayOrder);

/// <summary>
/// Creates a custom category the user can apply to transactions, either as a root category or a
/// child of one. A child always inherits its parent's nature, regardless of what's requested.
/// </summary>
[RequiresPermission(FinancesPermissions.Categories.Create)]
public sealed class CreateUserCategoryCommand(CreateUserCategoryInput input)
    : CommandBase<CreateUserCategoryInput, UserCategoryDto>(input);
