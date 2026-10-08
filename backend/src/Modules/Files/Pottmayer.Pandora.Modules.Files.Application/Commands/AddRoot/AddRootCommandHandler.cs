using Pottmayer.Pandora.Modules.Files.Abstractions;
using Pottmayer.Pandora.Modules.Files.Application.Dtos;
using Pottmayer.Pandora.Modules.Files.Domain.Entities;
using Pottmayer.Pandora.Modules.Files.Domain.Errors;
using Pottmayer.Pandora.Modules.Files.Domain.Ports.Repositories;
using Pottmayer.Pandora.Modules.Identity.Abstractions.Ports;
using Pottmayer.Tars.Core.Cqrs.Commands;
using Pottmayer.Tars.Core.Primitives.Outcomes;
using Pottmayer.Tars.Data.Abstractions.UnitOfWork;

namespace Pottmayer.Pandora.Modules.Files.Application.Commands.AddRoot;

/// <summary>
/// Adds a root to one of the user's devices. The path is not checked here — the agent checks it on its
/// next scan and aborts with <c>root-unavailable</c> if it does not exist. Adding the path of a removed
/// root brings it back, with its catalog.
/// </summary>
public sealed class AddRootCommandHandler(IUnitOfWorkFactory factory, IDeviceReader devices)
    : CommandHandlerBase<AddRootCommand, RootDto>
{
    protected override async Task<Result<RootDto>> HandleAsync(AddRootCommand request, CancellationToken ct)
    {
        var input = request.Input;
        if (!Root.IsValidName(input.Name)) return Fail(FilesErrors.InvalidRootName);
        if (!Root.IsValidLocalPath(input.LocalPath)) return Fail(FilesErrors.InvalidLocalPath);

        var device = await devices.GetActiveAsync(input.UserId, input.DeviceId, ct);
        if (device is null) return Fail(FilesErrors.DeviceNotFound);

        // Windows and macOS file systems ignore case by default; Linux (and Android) do not.
        var caseSensitive = input.CaseSensitive ?? device.Platform is "linux" or "android";
        var localPath = input.LocalPath!.Trim();

        return await factory.ExecuteAsync<Result<RootDto>>(FilesModule.DatabaseKey, async (ctx, token) =>
        {
            var roots = ctx.AcquireRepository<IRootRepository>();
            var existing = await roots.FindByDevicePathAsync(device.Id, localPath, token);
            if (existing is { IsActive: true }) return FilesErrors.RootAlreadyExists;

            if (existing is not null)
            {
                existing.Restore(input.Name!, input.ScanTime, input.IncludeHidden, caseSensitive);
                return Result<RootDto>.Success(RootDto.From(existing));
            }

            var root = Root.Create(input.UserId, device.Id, input.Name!, localPath, caseSensitive, input.IncludeHidden, input.ScanTime);
            await roots.AddAsync(root, token);
            return Result<RootDto>.Success(RootDto.From(root));
        }, cancellationToken: ct);
    }
}
