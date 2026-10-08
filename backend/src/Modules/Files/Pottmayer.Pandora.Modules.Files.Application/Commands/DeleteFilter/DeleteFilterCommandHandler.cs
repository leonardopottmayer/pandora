using Pottmayer.Pandora.Modules.Files.Abstractions;
using Pottmayer.Pandora.Modules.Files.Domain.Errors;
using Pottmayer.Pandora.Modules.Files.Domain.Ports.Repositories;
using Pottmayer.Tars.Core.Cqrs.Commands;
using Pottmayer.Tars.Core.Primitives.Outcomes;
using Pottmayer.Tars.Data.Abstractions.UnitOfWork;

namespace Pottmayer.Pandora.Modules.Files.Application.Commands.DeleteFilter;

/// <summary>Built-in filters can be deleted too; disabling keeps them for later.</summary>
public sealed class DeleteFilterCommandHandler(IUnitOfWorkFactory factory)
    : CommandHandlerBase<DeleteFilterCommand, bool>
{
    protected override async Task<Result<bool>> HandleAsync(DeleteFilterCommand request, CancellationToken ct)
    {
        var input = request.Input;

        return await factory.ExecuteAsync<Result<bool>>(FilesModule.DatabaseKey, async (ctx, token) =>
        {
            var filters = ctx.AcquireRepository<IFilterRepository>();
            var filter = await filters.FindForUserAsync(input.FilterId, input.UserId, token);
            if (filter is null) return FilesErrors.FilterNotFound;

            await filters.RemoveAsync(filter, token);
            return Result<bool>.Success(true);
        }, cancellationToken: ct);
    }
}
