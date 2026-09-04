using Pottmayer.Pandora.Modules.Assistant.Abstractions;
using Pottmayer.Pandora.Modules.Assistant.Application.Dtos;
using Pottmayer.Pandora.Modules.Assistant.Domain.Ports.Repositories;
using Pottmayer.Pandora.Modules.Assistant.Domain.ValueObjects;
using Pottmayer.Tars.Core.Cqrs.Queries;
using Pottmayer.Tars.Core.Primitives.Outcomes;
using Pottmayer.Tars.Data.Abstractions.UnitOfWork;

namespace Pottmayer.Pandora.Modules.Assistant.Application.Queries.GetProfile;

/// <summary>
/// The user's assistant configuration. When they have never saved one, returns defaults (provider and
/// model from <see cref="AssistantDefaults"/>, disabled, balanced) so the settings screen can render.
/// </summary>
public sealed class GetProfileQueryHandler(IUnitOfWorkFactory factory)
    : QueryHandlerBase<GetProfileQuery, AssistantProfileDto>
{
    protected override async Task<Result<AssistantProfileDto>> HandleAsync(
        GetProfileQuery request, CancellationToken cancellationToken)
    {
        var profile = await factory.ExecuteAsync(AssistantModule.DatabaseKey, async (context, ct) =>
        {
            var repo = context.AcquireRepository<IAssistantProfileRepository>();
            return await repo.FindByUserAsync(request.Input.UserId, ct);
        }, cancellationToken: cancellationToken);

        var dto = profile is null
            ? new AssistantProfileDto(
                AssistantDefaults.Provider, AssistantDefaults.Model, false, null, ConfirmationLevel.Balanced.Value)
            : new AssistantProfileDto(
                profile.ChatProvider, profile.ChatModel, profile.IsEnabled, profile.LocaleOverride, profile.ConfirmationLevel.Value);

        return Ok(dto);
    }
}
