using Pottmayer.Pandora.Modules.Finances.Application.Dtos;
using Pottmayer.Tars.Core.Cqrs.Commands;

namespace Pottmayer.Pandora.Modules.Finances.Application.Commands.UpdateRecurringTransaction;

public sealed record UpdateRecurringTransactionInput(
    Guid UserId,
    Guid Id,
    string Name,
    decimal? Amount,
    bool AmountIsEstimate,
    string Description,
    string? Payee,
    Guid? SystemCategoryId,
    Guid? UserCategoryId,
    DateOnly? EndDate,
    int? MaxOccurrences,
    bool AutoPost,
    bool AutoGenerate);

public sealed class UpdateRecurringTransactionCommand(UpdateRecurringTransactionInput input)
    : CommandBase<UpdateRecurringTransactionInput, RecurringTransactionDto>(input);
