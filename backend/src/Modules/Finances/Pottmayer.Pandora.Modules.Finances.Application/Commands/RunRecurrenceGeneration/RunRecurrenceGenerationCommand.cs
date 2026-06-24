using Pottmayer.Tars.Core.Cqrs.Commands;

namespace Pottmayer.Pandora.Modules.Finances.Application.Commands.RunRecurrenceGeneration;

/// <param name="Today">Date used as the upper bound baseline; typically UTC today.</param>
/// <param name="HorizonDays">How many days beyond today to generate occurrences for.</param>
public sealed record RunRecurrenceGenerationInput(DateOnly Today, int HorizonDays = 30);

/// <summary>
/// Daily job that posts scheduled pending account transactions whose date has arrived, then walks
/// every active recurring template's schedule up to the horizon, generating an occurrence (auto-posted
/// transaction or inbox suggestion) for each due date that hasn't been generated yet.
/// </summary>
public sealed class RunRecurrenceGenerationCommand(RunRecurrenceGenerationInput input)
    : CommandBase<RunRecurrenceGenerationInput, int>(input);
