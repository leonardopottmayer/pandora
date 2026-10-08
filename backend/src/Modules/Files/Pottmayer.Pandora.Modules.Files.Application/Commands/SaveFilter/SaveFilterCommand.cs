using Pottmayer.Pandora.Modules.Files.Application.Catalog;
using Pottmayer.Pandora.Modules.Files.Application.Dtos;
using Pottmayer.Tars.Core.Cqrs.Commands;

namespace Pottmayer.Pandora.Modules.Files.Application.Commands.SaveFilter;

/// <summary>Creates a filter (<paramref name="FilterId"/> null) or replaces one.</summary>
public sealed record SaveFilterInput(Guid UserId, Guid? FilterId, FilterInput Filter);

public sealed class SaveFilterCommand(SaveFilterInput input)
    : CommandBase<SaveFilterInput, FilterDto>(input);
