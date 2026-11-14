using Pottmayer.Tars.Core.Cqrs.Commands;

namespace Pottmayer.Pandora.Modules.Finances.Application.Commands.RunRecurrenceGeneration;

/// <param name="Today">One date for every user (tests, a manual run); null takes each user's own day in their zone.</param>
/// <param name="HorizonDays">How many days beyond today to generate occurrences for.</param>
public sealed record RunRecurrenceGenerationInput(DateOnly? Today = null, int HorizonDays = 30);

/// <summary>
/// Daily job that posts scheduled pending account transactions whose date has arrived, then walks
/// every active recurring template's schedule up to the horizon, generating an occurrence (auto-posted
/// transaction or inbox suggestion) for each due date that hasn't been generated yet.
/// </summary>
public sealed class RunRecurrenceGenerationCommand(RunRecurrenceGenerationInput input)
    : CommandBase<RunRecurrenceGenerationInput, int>(input);
