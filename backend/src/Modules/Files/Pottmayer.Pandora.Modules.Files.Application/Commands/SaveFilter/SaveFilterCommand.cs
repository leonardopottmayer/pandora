using Pottmayer.Pandora.Modules.Files.Application.Catalog;
using Pottmayer.Pandora.Modules.Files.Application.Dtos;
using Pottmayer.Tars.Core.Cqrs.Commands;
using Pottmayer.Tars.Security.Authorization.Abstractions;
using Pottmayer.Pandora.Modules.Files.Abstractions;

namespace Pottmayer.Pandora.Modules.Files.Application.Commands.SaveFilter;

/// <summary>Creates a filter (<paramref name="FilterId"/> null) or replaces one.</summary>
public sealed record SaveFilterInput(Guid UserId, Guid? FilterId, FilterInput Filter);

[RequiresPermission(FilesPermissions.Filters.Update)]
public sealed class SaveFilterCommand(SaveFilterInput input)
    : CommandBase<SaveFilterInput, FilterDto>(input);
