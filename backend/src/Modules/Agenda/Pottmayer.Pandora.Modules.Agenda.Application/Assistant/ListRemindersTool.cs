using System.Globalization;
using System.Text;
using System.Text.Json;
using Pottmayer.Pandora.Modules.Agenda.Application.Queries.GetReminders;
using Pottmayer.Pandora.Modules.Agenda.Domain.ValueObjects;
using Pottmayer.Pandora.Modules.Assistant.Abstractions.Commands;
using Pottmayer.Tars.Core.Mediator.Abstractions;

namespace Pottmayer.Pandora.Modules.Agenda.Application.Assistant;

/// <summary>
/// <c>list_reminders</c>: the user's pending reminders — one-off ones by when they fire (a snooze counts),
/// then the repeating ones. Numbered so a follow-up can move, rename or cancel one; like the other lists, it
/// goes to the user and the history keeps a content-free recap.
/// </summary>
public sealed class ListRemindersTool(ISender sender) : IAssistantTool
{
    public AssistantCommandDescriptor Descriptor { get; } = new(
        Name: "list_reminders",
        Description: "Shows the user all their pending reminders, numbered. The list goes to the user directly; you will not see it. For what happens on a given day use list_agenda.",
        ParametersJsonSchema: """{ "type": "object", "properties": {} }""",
        Confirmation: ConfirmationPolicy.Never,
        Examples:
        [
            new AssistantCommandExample("what reminders do I have?", "{}"),
        ]);

    public string Describe(AssistantToolContext context, JsonElement arguments) =>
        context.Text("Mostrar seus lembretes?", "Show your reminders?");

    public async Task<AssistantCommandOutcome> ExecuteAsync(AssistantToolContext context, JsonElement arguments, CancellationToken ct = default)
    {
        var result = await sender.Send(new GetRemindersQuery(new GetRemindersInput(context.UserId)), ct);
        if (!result.IsSuccess)
            return AssistantCommandOutcome.Failed(string.Join("; ", result.Errors.Select(e => e.Message)));

        var pending = (result.Value ?? [])
            .Where(r => r.Status is not (nameof(ReminderStatus.Acknowledged) or nameof(ReminderStatus.Cancelled)))
            .OrderBy(r => r.Rrule is not null)
            .ThenBy(r => r.SnoozedUntil ?? r.RemindAt)
            .ToList();

        var recap = $"[list_reminders: {pending.Count} reminder(s) shown to the user; content withheld from you]";
        if (pending.Count == 0)
            return AssistantCommandOutcome.Ok(context.Text("Nenhum lembrete pendente.", "No pending reminders."), recap);

        var sb = new StringBuilder(context.Text("Seus lembretes:", "Your reminders:"));
        var listed = new List<ListedItem>(pending.Count);
        foreach (var r in pending)
        {
            listed.Add(new ListedItem("reminder", r.Id, r.Title));
            sb.Append('\n').Append(listed.Count).Append(". ");
            if (r.Rrule is null)
                sb.Append(context.FormatDateTime(r.SnoozedUntil ?? r.RemindAt)).Append(" · ").Append(r.Title);
            else
            {
                var time = TimeZoneInfo.ConvertTime(r.RemindAt, context.TimeZone).ToString("HH:mm", CultureInfo.InvariantCulture);
                sb.Append(r.Title).Append(context.Text($" · repete, às {time}", $" · repeats, at {time}"));
            }
        }

        return AssistantCommandOutcome.Ok(sb.ToString(), recap, listed);
    }
}
