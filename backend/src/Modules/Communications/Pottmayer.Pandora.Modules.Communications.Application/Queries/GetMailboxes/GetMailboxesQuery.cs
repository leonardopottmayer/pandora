using Pottmayer.Pandora.Modules.Communications.Application.Dtos;
using Pottmayer.Tars.Core.Cqrs.Queries;
using Pottmayer.Tars.Security.Authorization.Abstractions;
using Pottmayer.Pandora.Modules.Communications.Abstractions;

namespace Pottmayer.Pandora.Modules.Communications.Application.Queries.GetMailboxes;

public sealed record GetMailboxesInput(Guid UserId);

[RequiresPermission(CommunicationsPermissions.Mailboxes.Read)]
public sealed class GetMailboxesQuery(GetMailboxesInput input)
    : QueryBase<GetMailboxesInput, IReadOnlyList<MailboxDto>>(input);
