using Pottmayer.Tars.Core.Cqrs.Commands;

namespace Pottmayer.Pandora.Modules.Finances.Application.Commands.RunImportParsing;

public sealed record RunImportParsingInput;

/// <summary>Claims one received import file and processes it. Returns true if a file was processed.</summary>
public sealed class RunImportParsingCommand()
    : CommandBase<RunImportParsingInput, bool>(new RunImportParsingInput());
