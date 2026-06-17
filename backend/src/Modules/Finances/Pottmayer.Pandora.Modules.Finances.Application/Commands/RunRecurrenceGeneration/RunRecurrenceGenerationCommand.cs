using Pottmayer.Tars.Core.Cqrs.Commands;

namespace Pottmayer.Pandora.Modules.Finances.Application.Commands.RunRecurrenceGeneration;

/// <param name="Today">Date used as the upper bound baseline; typically UTC today.</param>
/// <param name="HorizonDays">How many days beyond today to generate occurrences for.</param>
public sealed record RunRecurrenceGenerationInput(DateOnly Today, int HorizonDays = 30);

public sealed class RunRecurrenceGenerationCommand(RunRecurrenceGenerationInput input)
    : CommandBase<RunRecurrenceGenerationInput, int>(input);
