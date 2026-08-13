using Pottmayer.Pandora.Modules.Notes.Application.Dtos;
using Pottmayer.Tars.Core.Cqrs.Queries;

namespace Pottmayer.Pandora.Modules.Notes.Application.Queries.GetAttachment;

public sealed record GetAttachmentInput(Guid AttachmentId);

/// <summary>Loads an attachment's bytes for an authenticated download.</summary>
public sealed class GetAttachmentQuery(GetAttachmentInput input)
    : QueryBase<GetAttachmentInput, AttachmentContentDto>(input);
