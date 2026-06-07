using Pottmayer.Pandora.Modules.Identity.Application.Dtos;
using Pottmayer.Tars.Core.Cqrs.Queries;

namespace Pottmayer.Pandora.Modules.Identity.Application.Queries.Mfa.GetMfaStatus;

public sealed record GetMfaStatusInput(Guid UserId);

public sealed class GetMfaStatusQuery(GetMfaStatusInput input)
    : QueryBase<GetMfaStatusInput, MfaStatusDto>(input);
