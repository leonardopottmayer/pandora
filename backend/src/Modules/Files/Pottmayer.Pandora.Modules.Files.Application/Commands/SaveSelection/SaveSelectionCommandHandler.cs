using Pottmayer.Pandora.Modules.Files.Abstractions;
using Pottmayer.Pandora.Modules.Files.Application.Catalog;
using Pottmayer.Pandora.Modules.Files.Application.Dtos;
using Pottmayer.Pandora.Modules.Files.Domain.Errors;
using Pottmayer.Pandora.Modules.Files.Domain.Ports.Repositories;
using Pottmayer.Pandora.Modules.Files.Domain.ValueObjects;
using Pottmayer.Tars.Core.Cqrs.Commands;
using Pottmayer.Tars.Core.Primitives.Outcomes;
using Pottmayer.Tars.Data.Abstractions.UnitOfWork;

namespace Pottmayer.Pandora.Modules.Files.Application.Commands.SaveSelection;

/// <summary>The change applies on the next scan: entries it leaves out become excluded, never deleted.</summary>
public sealed class SaveSelectionCommandHandler(IUnitOfWorkFactory factory)
    : CommandHandlerBase<SaveSelectionCommand, RootDto>
{
    protected override async Task<Result<RootDto>> HandleAsync(SaveSelectionCommand request, CancellationToken ct)
    {
        var input = request.Input;

        return await factory.ExecuteAsync<Result<RootDto>>(FilesModule.DatabaseKey, async (ctx, token) =>
        {
            var root = await ctx.AcquireRepository<IRootRepository>().FindForUserAsync(input.RootId, input.UserId, token);
            if (root is not { IsActive: true }) return FilesErrors.RootNotFound;

            var seen = new HashSet<string>(root.CaseSensitive ? StringComparer.Ordinal : StringComparer.OrdinalIgnoreCase);
            var marks = new List<(string, SelectionMode)>();
            foreach (var mark in input.Marks ?? [])
            {
                if (!RootRules.TryNormalize(mark.Path, out var path)) return FilesErrors.InvalidPath(mark.Path);
                if (!SelectionMode.IsSupported(mark.Mode)) return FilesErrors.InvalidSelectionMode;
                if (!seen.Add(path)) return FilesErrors.DuplicateMark(path);
                marks.Add((path, SelectionMode.FromValue(mark.Mode)));
            }

            root.ReplaceSelection(marks);
            return Result<RootDto>.Success(RootDto.From(root));
        }, cancellationToken: ct);
    }
}
