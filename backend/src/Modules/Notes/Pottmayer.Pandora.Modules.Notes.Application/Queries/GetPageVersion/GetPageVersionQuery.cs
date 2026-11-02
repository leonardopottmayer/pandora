using Pottmayer.Pandora.Modules.Notes.Application.Dtos;
using Pottmayer.Tars.Core.Cqrs.Queries;

namespace Pottmayer.Pandora.Modules.Notes.Application.Queries.GetPageVersion;

public sealed record GetPageVersionInput(Guid UserId, Guid PageId, long Number);

/// <summary>Reads one immutable version of a page owned by the user.</summary>
public sealed class GetPageVersionQuery(GetPageVersionInput input)
    : QueryBase<GetPageVersionInput, PageVersionDto>(input);
