using Pottmayer.Pandora.Modules.Files.Abstractions;
using Pottmayer.Pandora.Modules.Files.Application.Catalog;
using Pottmayer.Pandora.Modules.Files.Application.Dtos;
using Pottmayer.Pandora.Modules.Files.Domain.Entities;
using Pottmayer.Pandora.Modules.Files.Domain.Errors;
using Pottmayer.Pandora.Modules.Files.Domain.Ports.Repositories;
using Pottmayer.Pandora.Modules.Identity.Abstractions.Ports;
using Pottmayer.Tars.Core.Cqrs.Commands;
using Pottmayer.Tars.Core.Primitives.Outcomes;
using Pottmayer.Tars.Data.Abstractions.UnitOfWork;

namespace Pottmayer.Pandora.Modules.Files.Application.Commands.SaveFilter;

/// <summary>Like a selection change, a filter change applies on the next scan.</summary>
public sealed class SaveFilterCommandHandler(IUnitOfWorkFactory factory, IDeviceReader devices)
    : CommandHandlerBase<SaveFilterCommand, FilterDto>
{
    protected override async Task<Result<FilterDto>> HandleAsync(SaveFilterCommand request, CancellationToken ct)
    {
        var input = request.Input;

        return await factory.ExecuteAsync<Result<FilterDto>>(FilesModule.DatabaseKey, async (ctx, token) =>
        {
            var validated = await FilterInputs.ValidateAsync(input.Filter, input.UserId, ctx, devices, token);
            if (validated.IsFailure) return Result<FilterDto>.Failure(validated.Errors);

            var filters = ctx.AcquireRepository<IFilterRepository>();
            if (input.FilterId is not { } id)
            {
                var created = Filter.Create(input.UserId, validated.Value!);
                await filters.AddAsync(created, token);
                return Result<FilterDto>.Success(FilterDto.From(created));
            }

            var filter = await filters.FindForUserAsync(id, input.UserId, token);
            if (filter is null) return FilesErrors.FilterNotFound;

            filter.Update(validated.Value!);
            return Result<FilterDto>.Success(FilterDto.From(filter));
        }, cancellationToken: ct);
    }
}
