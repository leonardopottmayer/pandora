using System.Globalization;
using System.Text.Json;
using Pottmayer.Pandora.Modules.Agenda.Application.Commands.CreateReminder;
using Pottmayer.Pandora.Modules.Assistant.Abstractions.Commands;
using Pottmayer.Tars.Core.Mediator.Abstractions;

namespace Pottmayer.Pandora.Modules.Agenda.Application.Assistant;

/// <summary>
/// The Agenda's contribution to the assistant catalog: the <c>create_reminder</c> tool. Thin by design —
/// it parses the model's arguments and forwards them to the existing <see cref="CreateReminderCommand"/>
/// through the mediator, duplicating none of its rules. The reminder's own use case resolves the time
/// zone and validates the request; this tool only shapes the call and the reply.
/// </summary>
public sealed class CreateReminderTool(ISender sender) : IAssistantTool
{
    public AssistantCommandDescriptor Descriptor { get; } = new(
        Name: "create_reminder",
        Description: "Creates a one-off reminder with a title and a time. Use when the user asks to be reminded of something.",
        ParametersJsonSchema: """
        {
          "type": "object",
          "properties": {
            "title": { "type": "string", "description": "What to be reminded of, in a few words." },
            "remindAt": { "type": "string", "description": "When to remind, as an absolute ISO-8601 timestamp with offset (e.g. 2026-09-05T10:00:00-03:00)." }
          },
          "required": ["title", "remindAt"]
        }
        """,
        Confirmation: ConfirmationPolicy.WhenAmbiguous,
        Examples:
        [
            new AssistantCommandExample(
                "remind me to call the dentist tomorrow at 9",
                """{ "title": "Call the dentist", "remindAt": "2026-09-05T09:00:00-03:00" }"""),
            new AssistantCommandExample(
                "reminder to pay the rent on the 5th at 10am",
                """{ "title": "Pay the rent", "remindAt": "2026-09-05T10:00:00-03:00" }"""),
        ]);

    public async Task<AssistantCommandOutcome> ExecuteAsync(Guid userId, JsonElement arguments, CancellationToken ct = default)
    {
        if (!arguments.TryGetProperty("title", out var titleElement) || titleElement.ValueKind != JsonValueKind.String)
            throw new ArgumentException("The 'title' argument is required.");

        if (!arguments.TryGetProperty("remindAt", out var remindAtElement) || remindAtElement.ValueKind != JsonValueKind.String)
            throw new ArgumentException("The 'remindAt' argument is required.");

        var title = titleElement.GetString()!;
        var remindAt = DateTimeOffset.Parse(
            remindAtElement.GetString()!, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);

        var command = new CreateReminderCommand(new CreateReminderInput(
            userId, title, Notes: null, remindAt, TimeZone: null));
        var result = await sender.Send(command, ct);

        if (!result.IsSuccess)
            return AssistantCommandOutcome.Failed(string.Join("; ", result.Errors.Select(e => e.Message)));

        var dto = result.Value!;
        return AssistantCommandOutcome.Ok($"Reminder \"{dto.Title}\" created for {FormatLocal(dto.RemindAt, dto.TimeZone)}.");
    }

    private static string FormatLocal(DateTimeOffset instant, string ianaTimeZone)
    {
        var culture = CultureInfo.GetCultureInfo("en-US");
        try
        {
            var tz = TimeZoneInfo.FindSystemTimeZoneById(ianaTimeZone);
            instant = TimeZoneInfo.ConvertTime(instant, tz);
        }
        catch (TimeZoneNotFoundException)
        {
            // Fall back to the instant as given.
        }

        return instant.ToString("MMM d, yyyy 'at' HH:mm", culture);
    }
}
