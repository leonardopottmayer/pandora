using Pottmayer.Pandora.Modules.Files.Abstractions;
using Pottmayer.Pandora.Modules.Files.Application.Dtos;
using Pottmayer.Pandora.Modules.Files.Domain.Entities;
using Pottmayer.Pandora.Modules.Files.Domain.Errors;
using Pottmayer.Pandora.Modules.Files.Domain.Ports.Repositories;
using Pottmayer.Tars.Core.Cqrs.Commands;
using Pottmayer.Tars.Core.Primitives.Outcomes;
using Pottmayer.Tars.Data.Abstractions.UnitOfWork;

namespace Pottmayer.Pandora.Modules.Files.Application.Commands.UpdateRoot;

public sealed class UpdateRootCommandHandler(IUnitOfWorkFactory factory)
    : CommandHandlerBase<UpdateRootCommand, RootDto>
{
    protected override async Task<Result<RootDto>> HandleAsync(UpdateRootCommand request, CancellationToken ct)
    {
        var input = request.Input;
        if (!Root.IsValidName(input.Name)) return Fail(FilesErrors.InvalidRootName);

        return await factory.ExecuteAsync<Result<RootDto>>(FilesModule.DatabaseKey, async (ctx, token) =>
        {
            var root = await ctx.AcquireRepository<IRootRepository>().FindForUserAsync(input.RootId, input.UserId, token);
            if (root is not { IsActive: true }) return FilesErrors.RootNotFound;

            root.Update(input.Name!, input.ScanTime, input.IncludeHidden, input.CaseSensitive);
            return Result<RootDto>.Success(RootDto.From(root));
        }, cancellationToken: ct);
    }
}
