using Pottmayer.Pandora.Modules.Finances.Application.Dtos;
using Pottmayer.Tars.Core.Cqrs.Queries;
using Pottmayer.Tars.Security.Authorization.Abstractions;
using Pottmayer.Pandora.Modules.Finances.Abstractions;

namespace Pottmayer.Pandora.Modules.Finances.Application.Queries.GetAttachmentContent;

public sealed record GetAttachmentContentInput(Guid UserId, Guid AttachmentId);

/// <summary>An attachment's bytes, for one of the user's own attachments only.</summary>
[RequiresPermission(FinancesPermissions.Attachments.Read)]
public sealed class GetAttachmentContentQuery(GetAttachmentContentInput input)
    : QueryBase<GetAttachmentContentInput, AttachmentContentDto>(input);
