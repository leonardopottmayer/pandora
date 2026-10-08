using Pottmayer.Pandora.Modules.Files.Application.Dtos;
using Pottmayer.Tars.Core.Cqrs.Queries;

namespace Pottmayer.Pandora.Modules.Files.Application.Queries.GetEntry;

public sealed record GetEntryInput(Guid UserId, Guid EntryId);

public sealed class GetEntryQuery(GetEntryInput input)
    : QueryBase<GetEntryInput, EntryDto>(input);
