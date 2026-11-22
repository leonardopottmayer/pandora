using System.Text.Json;
using Pottmayer.Pandora.Modules.Agenda.Application.Assistant;
using Pottmayer.Pandora.Modules.Assistant.Abstractions.Commands;
using Xunit;

namespace Pottmayer.Pandora.Modules.Agenda.Tests;

public sealed class CreateReminderToolTests
{
    private static readonly TimeZoneInfo SaoPaulo = TimeZoneInfo.FindSystemTimeZoneById("America/Sao_Paulo");

    // Describe never touches the mediator.
    private readonly CreateReminderTool _tool = new(sender: null!);

    private static JsonElement Args(string json) => JsonDocument.Parse(json).RootElement.Clone();

    [Fact]
    public void Describes_the_reminder_in_the_users_language_and_zone()
    {
        var args = Args("""{ "title": "Pagar o aluguel", "remindAt": "2026-09-05T13:00:00Z" }""");

        Assert.Equal(
            "Criar o lembrete \"Pagar o aluguel\" para 05/09/2026 às 10:00?",
            _tool.Describe(new AssistantToolContext(Guid.NewGuid(), "pt-BR", SaoPaulo), args));
        Assert.Equal(
            "Create the reminder \"Pagar o aluguel\" for Sep 5, 2026 at 10:00?",
            _tool.Describe(new AssistantToolContext(Guid.NewGuid(), "en-US", SaoPaulo), args));
    }

    [Fact]
    public void Describing_unreadable_arguments_throws_so_the_pipeline_rejects_the_call()
    {
        Assert.Throws<ArgumentException>(() =>
            _tool.Describe(new AssistantToolContext(Guid.NewGuid(), "pt-BR", SaoPaulo), Args("""{ "title": "x" }""")));
    }
}
