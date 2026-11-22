using Pottmayer.Pandora.Modules.Communications.Application.Dtos;
using Pottmayer.Tars.Core.Cqrs.Queries;
using Pottmayer.Tars.Security.Authorization.Abstractions;
using Pottmayer.Pandora.Modules.Communications.Abstractions;

namespace Pottmayer.Pandora.Modules.Communications.Application.Queries.GetLabels;

public sealed record GetLabelsInput(Guid UserId, Guid MailboxId);

/// <summary>System labels first, then the user's, by name.</summary>
[RequiresPermission(CommunicationsPermissions.Labels.Read)]
public sealed class GetLabelsQuery(GetLabelsInput input)
    : QueryBase<GetLabelsInput, IReadOnlyList<LabelDto>>(input);
