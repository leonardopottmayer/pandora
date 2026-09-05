using Pottmayer.Pandora.Modules.Assistant.Application.Dtos;
using Pottmayer.Tars.Core.Cqrs.Queries;

namespace Pottmayer.Pandora.Modules.Assistant.Application.Queries.GetInvocations;

public sealed record GetInvocationsInput(Guid UserId, int Limit);

public sealed class GetInvocationsQuery(GetInvocationsInput input)
    : QueryBase<GetInvocationsInput, IReadOnlyList<InvocationDto>>(input);
