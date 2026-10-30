using Pottmayer.Pandora.Modules.Communications.Application.Dtos;
using Pottmayer.Tars.Core.Cqrs.Queries;

namespace Pottmayer.Pandora.Modules.Communications.Application.Queries.GetAttachment;

public sealed record GetAttachmentInput(Guid UserId, Guid MessageId, string AttachmentId);

/// <summary>One part of the message, decoded. Also serves inline (<c>cid:</c>) images.</summary>
public sealed class GetAttachmentQuery(GetAttachmentInput input)
    : QueryBase<GetAttachmentInput, AttachmentFile>(input);
