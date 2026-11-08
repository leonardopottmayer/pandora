using Pottmayer.Pandora.Modules.Notes.Application.Commands.PageSettings;
using Pottmayer.Pandora.Modules.Notes.Application.Dtos;
using Pottmayer.Tars.Core.Cqrs.Commands;
using Pottmayer.Tars.Core.Primitives.Outcomes;
using Pottmayer.Tars.Data.Abstractions.UnitOfWork;

namespace Pottmayer.Pandora.Modules.Notes.Application.Commands.SetPageTemplate;

public sealed class SetPageTemplateCommandHandler(IUnitOfWorkFactory factory) : CommandHandlerBase<SetPageTemplateCommand, PageDto>
{
    protected override async Task<Result<PageDto>> HandleAsync(SetPageTemplateCommand request, CancellationToken ct)
    {
        var input = request.Input;
        var result = await PageSettingsChange.RunAsync(factory, input.UserId, input.PageId, (page, _, _) =>
        {
            page.SetTemplate(input.IsTemplate);
            return Task.CompletedTask;
        }, ct);
        return result.IsFailure ? Fail([.. result.Errors]) : Ok(result.Value!);
    }
}
