using Pottmayer.Pandora.Modules.Integrations.Abstractions;
using Pottmayer.Pandora.Modules.Integrations.Domain.Errors;
using Pottmayer.Pandora.Modules.Integrations.Domain.Ports.Repositories;
using Pottmayer.Tars.Core.Cqrs.Commands;
using Pottmayer.Tars.Core.Primitives.Outcomes;
using Pottmayer.Tars.Data.Abstractions.UnitOfWork;

namespace Pottmayer.Pandora.Modules.Integrations.Application.Commands.RenameAccount;

public sealed class RenameAccountCommandHandler(IUnitOfWorkFactory factory)
    : CommandHandlerBase<RenameAccountCommand, bool>
{
    protected override async Task<Result<bool>> HandleAsync(RenameAccountCommand request, CancellationToken ct)
    {
        var input = request.Input;
        if (string.IsNullOrWhiteSpace(input.Label))
            return Fail(IntegrationErrors.LabelRequired);

        return await factory.ExecuteAsync(IntegrationsModule.DatabaseKey, async (context, token) =>
        {
            var repo = context.AcquireRepository<IExternalAccountRepository>();
            var account = await repo.GetOwnedAsync(input.UserId, input.AccountId, token);
            if (account is null)
                return Result<bool>.Failure(IntegrationErrors.AccountNotFound);

            account.Rename(input.Label);
            await repo.UpdateAsync(account, token);
            return Result<bool>.Success(true);
        }, cancellationToken: ct);
    }
}
