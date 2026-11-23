using Pottmayer.Pandora.Modules.Finances.Abstractions;
using Pottmayer.Pandora.Modules.Finances.Application.Dtos;
using Pottmayer.Tars.Core.Cqrs.Commands;
using Pottmayer.Tars.Security.Authorization.Abstractions;

namespace Pottmayer.Pandora.Modules.Finances.Application.Commands.ReportPositionValues;

public sealed record PositionValueItem(Guid PositionId, decimal Value);

/// <summary>One or many values, all on the same date. A value on a date that already has one replaces it.</summary>
public sealed record ReportPositionValuesInput(Guid UserId, DateOnly ValuedOn, IReadOnlyList<PositionValueItem> Items);

/// <summary>Reports what positions are worth on a date (the "Update values" screen and the single report).</summary>
[RequiresPermission(FinancesPermissions.Investments.Update)]
public sealed class ReportPositionValuesCommand(ReportPositionValuesInput input)
    : CommandBase<ReportPositionValuesInput, IReadOnlyList<PositionValuationDto>>(input);
