using System.Globalization;
using Pottmayer.Pandora.Modules.Identity.Abstractions.Ports;
using Pottmayer.Pandora.Modules.Notes.Abstractions;
using Pottmayer.Pandora.Modules.Notes.Application.Commands.CreatePage;
using Pottmayer.Pandora.Modules.Notes.Application.Dtos;
using Pottmayer.Pandora.Modules.Notes.Domain.Errors;
using Pottmayer.Pandora.Modules.Notes.Domain.Ports.Repositories;
using Pottmayer.Pandora.Modules.Notes.Domain.ValueObjects;
using Pottmayer.Tars.Core.Cqrs.Commands;
using Pottmayer.Tars.Core.Mediator.Abstractions;
using Pottmayer.Tars.Core.Primitives.Outcomes;
using Pottmayer.Tars.Data.Abstractions.UnitOfWork;

namespace Pottmayer.Pandora.Modules.Notes.Application.Commands.CreatePageFromTemplate;

public sealed class CreatePageFromTemplateCommandHandler(
    IUnitOfWorkFactory factory, ISender sender, IEffectiveTimeZoneResolver timeZones, TimeProvider timeProvider)
    : CommandHandlerBase<CreatePageFromTemplateCommand, PageDto>
{
    protected override async Task<Result<PageDto>> HandleAsync(CreatePageFromTemplateCommand request, CancellationToken ct)
    {
        var input = request.Input;
        if (string.IsNullOrWhiteSpace(input.Title))
            return Fail(PageErrors.InvalidTitle);

        var template = await factory.ExecuteAsync(NotesModule.DatabaseKey, (ctx, token) =>
            ctx.AcquireRepository<IPageRepository>().FindByIdForUserAsync(input.TemplateId, input.UserId, token), cancellationToken: ct);
        if (template is null || !template.IsTemplate || template.IsArchived)
            return Fail(PageErrors.NotFound);

        var today = await timeZones.ResolveTodayAsync(input.UserId, timeProvider.GetUtcNow(), ct);
        var title = input.Title.Trim().Replace("{{date}}", today.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
        var content = PageContent.Parse(template.Content).InstantiateTemplate(title, today);
        // Reuse creation's destination checks and its history, links, tags, media and block stamps.
        return await sender.Send(new CreatePageCommand(new CreatePageInput(
            input.UserId, title, null, template.Icon, content.Json, input.SpaceId, input.FolderId)), ct);
    }
}
