using Pottmayer.Pandora.Modules.Communications.Application.Dtos;
using Pottmayer.Tars.Core.Cqrs.Queries;
using Pottmayer.Tars.Security.Authorization.Abstractions;
using Pottmayer.Pandora.Modules.Communications.Abstractions;

namespace Pottmayer.Pandora.Modules.Communications.Application.Queries.GetAttachment;

public sealed record GetAttachmentInput(Guid UserId, Guid MessageId, string AttachmentId);

/// <summary>One part of the message, decoded. Also serves inline (<c>cid:</c>) images.</summary>
[RequiresPermission(CommunicationsPermissions.Messages.Read)]
public sealed class GetAttachmentQuery(GetAttachmentInput input)
    : QueryBase<GetAttachmentInput, AttachmentFile>(input);
