using Pottmayer.Pandora.Modules.Assistant.Abstractions;
using Pottmayer.Pandora.Modules.Assistant.Domain.Aggregates;
using Pottmayer.Pandora.Modules.Assistant.Domain.Errors;
using Pottmayer.Pandora.Modules.Assistant.Domain.Ports.Repositories;
using Pottmayer.Pandora.Modules.Assistant.Domain.ValueObjects;
using Pottmayer.Tars.Core.Cqrs.Commands;
using Pottmayer.Tars.Core.Primitives.Outcomes;
using Pottmayer.Tars.Data.Abstractions.UnitOfWork;

namespace Pottmayer.Pandora.Modules.Assistant.Application.Commands.SaveProfile;

/// <summary>
/// Upserts the user's assistant profile on (user): a first save creates it, a later save replaces the
/// mutable configuration.
/// </summary>
public sealed class SaveProfileCommandHandler(IUnitOfWorkFactory factory, TimeProvider timeProvider)
    : CommandHandlerBase<SaveProfileCommand, bool>
{
    protected override async Task<Result<bool>> HandleAsync(SaveProfileCommand request, CancellationToken ct)
    {
        var input = request.Input;

        var model = input.Model?.Trim();
        if (string.IsNullOrEmpty(model))
            return Fail(AssistantErrors.ModelRequired);

        ConfirmationLevel level;
        try
        {
            level = ConfirmationLevel.FromValue(input.ConfirmationLevel);
        }
        catch (ArgumentOutOfRangeException)
        {
            return Fail(AssistantErrors.UnknownConfirmationLevel(input.ConfirmationLevel));
        }

        var provider = string.IsNullOrWhiteSpace(input.Provider) ? AssistantDefaults.Provider : input.Provider.Trim();
        var localeOverride = string.IsNullOrWhiteSpace(input.LocaleOverride) ? null : input.LocaleOverride.Trim();

        await factory.ExecuteAsync(AssistantModule.DatabaseKey, async (context, token) =>
        {
            var repo = context.AcquireRepository<IAssistantProfileRepository>();
            var existing = await repo.FindByUserAsync(input.UserId, token);

            if (existing is null)
            {
                var profile = AssistantProfile.Create(
                    input.UserId, provider, model, input.IsEnabled, localeOverride, level, timeProvider);
                await repo.AddAsync(profile, token);
            }
            else
            {
                existing.Update(provider, model, input.IsEnabled, localeOverride, level);
                await repo.UpdateAsync(existing, token);
            }

            return true;
        }, cancellationToken: ct);

        return Ok(true);
    }
}
