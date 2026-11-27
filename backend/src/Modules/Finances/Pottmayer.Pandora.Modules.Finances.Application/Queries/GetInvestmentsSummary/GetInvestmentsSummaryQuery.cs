using Pottmayer.Pandora.Modules.Finances.Abstractions;
using Pottmayer.Pandora.Modules.Finances.Application.Dtos;
using Pottmayer.Tars.Core.Cqrs.Queries;
using Pottmayer.Tars.Security.Authorization.Abstractions;

namespace Pottmayer.Pandora.Modules.Finances.Application.Queries.GetInvestmentsSummary;

public sealed record GetInvestmentsSummaryInput(Guid UserId);

/// <summary>Open positions totalled per currency, by class and by account, plus the next maturities.</summary>
[RequiresPermission(FinancesPermissions.Investments.Read)]
public sealed class GetInvestmentsSummaryQuery(GetInvestmentsSummaryInput input) : QueryBase<GetInvestmentsSummaryInput, InvestmentsSummaryDto>(input);
