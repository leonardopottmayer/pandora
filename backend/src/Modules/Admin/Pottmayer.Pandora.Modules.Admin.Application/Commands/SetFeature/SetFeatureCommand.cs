using Pottmayer.Pandora.Modules.Admin.Application.Dtos;
using Pottmayer.Pandora.Modules.Admin.Abstractions;
using Pottmayer.Tars.Core.Cqrs.Commands;
using Pottmayer.Tars.Security.Authorization.Abstractions;

namespace Pottmayer.Pandora.Modules.Admin.Application.Commands.SetFeature;

/// <param name="UserId">The administrator making the change.</param>
public sealed record SetFeatureInput(Guid UserId, string Key, bool Enabled);

/// <summary>Switches a flag on or off for everyone. Back at its default, the override row is removed.</summary>
[RequiresPermission(AdminPermissions.Features.Manage)]
public sealed class SetFeatureCommand(SetFeatureInput input)
    : CommandBase<SetFeatureInput, IReadOnlyList<FeatureDto>>(input);
