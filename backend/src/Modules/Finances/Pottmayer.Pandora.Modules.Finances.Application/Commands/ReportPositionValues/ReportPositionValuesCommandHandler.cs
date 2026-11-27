using Pottmayer.Pandora.Modules.Finances.Abstractions;
using Pottmayer.Pandora.Modules.Finances.Application.Auditing;
using Pottmayer.Pandora.Modules.Finances.Application.Dtos;
using Pottmayer.Pandora.Modules.Finances.Application.Services;
using Pottmayer.Pandora.Modules.Finances.Domain.Aggregates;
using Pottmayer.Pandora.Modules.Finances.Domain.Errors;
using Pottmayer.Pandora.Modules.Finances.Domain.Ports.Repositories;
using Pottmayer.Pandora.Modules.Finances.Domain.ValueObjects;
using Pottmayer.Pandora.Modules.Identity.Abstractions.Ports;
using Pottmayer.Tars.Core.Cqrs.Commands;
using Pottmayer.Tars.Core.Cqrs.Queries;
using Pottmayer.Tars.Core.Primitives.Outcomes;
using Pottmayer.Tars.Data.Abstractions.DataContext;
using Pottmayer.Tars.Data.Abstractions.UnitOfWork;

namespace Pottmayer.Pandora.Modules.Finances.Application.Commands.ReportPositionValues;

public sealed class ReportPositionValuesCommandHandler(IUnitOfWorkFactory factory, TimeProvider timeProvider)
    : CommandHandlerBase<ReportPositionValuesCommand, IReadOnlyList<PositionValuationDto>>
{
    protected override async Task<Result<IReadOnlyList<PositionValuationDto>>> HandleAsync(ReportPositionValuesCommand request, CancellationToken ct)
    {
        var input = request.Input;
        if (input.Items.Count == 0 || input.Items.Any(i => i.Value < 0m)) return Fail(PositionErrors.InvalidAmount);

        var now = timeProvider.GetUtcNow();
        var result = await factory.ExecuteAsync(FinancesModule.DatabaseKey, async (ctx, token) =>
        {
            var positions = ctx.AcquireRepository<IPositionRepository>();
            var values = ctx.AcquireRepository<IPositionValuationRepository>();
            var correlationId = Guid.CreateVersion7();
            var saved = new List<PositionValuationDto>();

            foreach (var item in input.Items)
            {
                var position = await positions.FindByIdForUserAsync(item.PositionId, input.UserId, token);
                if (position is null) return Result<IReadOnlyList<PositionValuationDto>>.Failure([PositionErrors.NotFound]);
                if (!position.IsOpen) return Result<IReadOnlyList<PositionValuationDto>>.Failure([PositionErrors.Closed]);

                var existing = await values.FindOnDateAsync(position.Id, input.ValuedOn, token);
                decimal? old = existing?.Value;
                if (existing is null)
                {
                    existing = PositionValuation.Create(input.UserId, position.Id, input.ValuedOn, item.Value, ValuationSource.Manual, timeProvider);
                    await values.AddAsync(existing, token);
                }
                else
                {
                    existing.Replace(item.Value, ValuationSource.Manual);
                    await values.UpdateAsync(existing, token);
                }

                await ctx.RecordAsync(input.UserId, input.UserId, PositionEvents.EntityType, position.Id, PositionEvents.Valued, now,
                    new { valuedOn = input.ValuedOn, value = new { old, @new = existing.Value } }, correlationId, token);
                saved.Add(PositionValuationDto.From(existing));
            }

            return Result<IReadOnlyList<PositionValuationDto>>.Success(saved);
        }, cancellationToken: ct);

        return result.IsFailure ? Fail([.. result.Errors]) : Ok(result.Value!);
    }
}
