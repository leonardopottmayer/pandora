using Pottmayer.Pandora.Modules.Files.Application.Dtos;
using Pottmayer.Tars.Core.Cqrs.Queries;
using Pottmayer.Tars.Security.Authorization.Abstractions;
using Pottmayer.Pandora.Modules.Files.Abstractions;

namespace Pottmayer.Pandora.Modules.Files.Application.Queries.GetEntry;

public sealed record GetEntryInput(Guid UserId, Guid EntryId);

[RequiresPermission(FilesPermissions.Entries.Read)]
public sealed class GetEntryQuery(GetEntryInput input)
    : QueryBase<GetEntryInput, EntryDto>(input);
