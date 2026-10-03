using System.Diagnostics;
using System.Text;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Pottmayer.Pandora.Modules.Channels.Contracts;
using Pottmayer.Tars.Messaging.Abstractions;
using Xunit;
using Xunit.Abstractions;

namespace Pottmayer.Pandora.IntegrationTests.Live;

/// <summary>
/// Talks to the assistant the way Telegram does — an <see cref="InboundMessageReceived"/> on the assistant
/// bot, replies through Channels, button taps as <see cref="InboundInteractionReceived"/> — against the real
/// Gemini, scenario by scenario on a freshly seeded database, and writes what happened to a Markdown report
/// (<c>PANDORA_LIVE_REPORT</c>, default in the temp folder). It asserts nothing about the model's choices: the
/// report is for reading. Steps: plain text is sent; <c>!confirm</c>/<c>!cancel</c> tap the first such button
/// of the last reply; <c>?</c> + SQL records a query's rows.
/// </summary>
[Collection("AssistantLive")]
public sealed class AssistantLiveTests(AssistantLiveFactory factory, ITestOutputHelper output)
{
    private static readonly Guid UserId = Guid.Parse("01a0ea16-af53-7668-b48c-8dcd09860fd5");

    private sealed record Scenario(string Name, params string[] Steps);

    private static readonly Scenario[] Scenarios =
    [
        // ── Agenda: reading ───────────────────────────────────────────────────────────────────────
        new("Agenda de hoje", "o que tenho hoje?"),
        new("Agenda da semana", "o que tenho essa semana?"),
        new("Semana que vem", "e semana que vem, como está?"),
        new("Tarefas e follow-up", "minhas tarefas", "e quais estão atrasadas?"),
        new("Tarefas de uma lista", "tarefas da lista Casa"),
        new("Concluídas", "o que eu já concluí?"),
        new("Coloquial", "e aí, o que eu tenho pra fazer hoje?"),
        new("Inglês", "what do I have tomorrow?"),

        // ── Agenda: pointing by number ────────────────────────────────────────────────────────────
        new("Ref: excluir evento da semana", "o que tenho essa semana?", "exclui o 3", "!confirm"),
        new("Ref: duas ações numa frase", "quais tarefas estão atrasadas?", "conclui a 1 e adia a 2 pra segunda"),
        new("Ref: o alvo não muda com lista nova", "minhas tarefas", "exclui a 2", "o que tenho hoje?", "!confirm",
            "?SELECT title, deleted_at IS NOT NULL AS deleted FROM agenda.agd005_task WHERE deleted_at IS NOT NULL"),
        new("Ref: o modelo não vê o conteúdo", "minhas tarefas", "qual é o nome da tarefa 2?"),
        new("Ref: tipo errado", "o que tenho hoje?", "conclui o 1"),
        new("Ref: número fora da lista", "minhas tarefas", "exclui a 40"),

        // ── Agenda: changes by name ───────────────────────────────────────────────────────────────
        new("Mover evento", "muda a reunião com o contador pra segunda às 9h",
            "?SELECT title, starts_at AT TIME ZONE 'America/Sao_Paulo' AS starts, ends_at AT TIME ZONE 'America/Sao_Paulo' AS ends FROM agenda.agd002_event WHERE title ILIKE '%contador%'"),
        new("Mudar só o horário", "o jantar da Ana vai ser às 20h"),
        new("Cancelar uma ocorrência", "cancela a daily de segunda", "!confirm",
            "?SELECT original_starts_at AT TIME ZONE 'America/Sao_Paulo' AS original, is_cancelled FROM agenda.agd003_event_occurrence_override ORDER BY original_starts_at"),
        new("Série: só desta vez", "o futebol de quinta que vem vai ser às 21h, só dessa vez"),
        new("Série: daqui pra frente", "a partir de agora o futebol é às 19h",
            "?SELECT title, starts_at AT TIME ZONE 'America/Sao_Paulo' AS starts, rrule, recurrence_ends_at FROM agenda.agd002_event WHERE title = 'Futebol'"),
        new("Série: excluir tudo, cancelado", "exclui o futebol de vez", "!cancel",
            "?SELECT count(*) AS live_football FROM agenda.agd002_event WHERE title = 'Futebol' AND deleted_at IS NULL"),
        new("Renomear tarefa", "muda o nome da tarefa renovar CNH pra renovar CNH e RG"),
        new("Prazo e prioridade juntos", "a tarefa do presente da Ana é pra amanhã, prioridade alta",
            "?SELECT title, due_at AT TIME ZONE 'America/Sao_Paulo' AS due, priority FROM agenda.agd005_task WHERE title ILIKE '%presente%'"),
        new("Remarcar lembrete", "remarca o lembrete ligar pra mãe pra domingo às 18h",
            "?SELECT title, status, remind_at AT TIME ZONE 'America/Sao_Paulo' AS remind_at, snoozed_until FROM agenda.agd006_reminder WHERE title ILIKE '%mãe%'"),
        new("Renomear lembrete recorrente", "renomeia o lembrete do remédio pra tomar vitamina D"),
        new("Adiar lembrete recorrente", "adia o lembrete do remédio pra 22h"),
        new("Cancelar lembrete", "cancela o lembrete de levar o carro na revisão", "!confirm"),
        new("Reabrir tarefa", "reabre a tarefa trocar lâmpada"),
        new("Excluir subtarefa", "exclui a tarefa pedir três orçamentos", "!confirm"),

        // ── Agenda: creating ──────────────────────────────────────────────────────────────────────
        new("Criar lembrete", "me lembra de pagar o IPTU dia 15 às 10h"),
        new("Criar duas coisas", "cria uma tarefa comprar ração e um evento consulta veterinária sábado às 11h"),
        new("Faltou informação", "marca dentista"),

        // ── Finances ──────────────────────────────────────────────────────────────────────────────
        new("Gasto do mês", "quanto gastei esse mês?"),
        new("Por categoria vs anterior", "quanto gastei no mês passado por categoria, comparado com o anterior?"),
        new("Cartão Nubank", "quanto gastei no cartão Nubank em setembro?"),
        new("Conta Nubank", "quanto saiu da conta do Nubank em setembro?"),
        new("Itaú sem dizer qual", "gastos no Itaú esse mês"),
        new("iFood mês a mês", "quanto gastei com iFood nos últimos 3 meses, mês a mês?"),
        new("Maiores gastos", "meus 5 maiores gastos dos últimos 30 dias"),
        new("Categoria-mãe", "quanto gastei com alimentação esse ano?"),
        new("Categoria do usuário", "quanto gastei com marmitas?"),
        new("Receitas", "quanto recebi em setembro?"),
        new("Saldos e cartões", "quanto tenho nas contas?", "e nos cartões?"),
        new("Inbox e aprovação", "tem algo pra revisar no Finances?", "aprova o primeiro"),
        new("Registrar gasto", "gastei 47,90 na farmácia no cartão Nubank"),
        new("Pagar fatura", "paga a fatura do Itaú"),

        // ── Notes ─────────────────────────────────────────────────────────────────────────────────
        new("Buscar, abrir, acrescentar", "procura minhas notas sobre ideias", "abre a 1", "acrescenta na 2: app de controle de plantas",
            "?SELECT title, right(content_markdown, 60) AS ending FROM notes.nte001_page WHERE content_markdown ILIKE '%plantas%'"),
        new("Nota longa", "abre a nota anotações hábitos atômicos"),
        new("Criar nota", "anota: a senha do portão é 4321"),
        new("Acrescentar por nome", "acrescenta na nota do imposto de renda: pedir informe da corretora",
            "?SELECT right(content_markdown, 80) AS ending FROM notes.nte001_page WHERE title ILIKE 'Imposto%'"),
        new("Nota arquivada", "procura lista de compras"),
        new("Editar o meio da nota", "muda o segundo passo da receita do pão para 3 dobras"),

        // ── More ways people talk ─────────────────────────────────────────────────────────────────
        new("Lembrete relativo", "me lembra de tirar a roupa da máquina daqui a 40 minutos"),
        new("Tarefa com hora", "cria a tarefa ligar pro banco amanhã às 14h"),
        new("Corrigir logo depois", "me lembra de comprar pão amanhã às 8h", "na verdade às 9h",
            "?SELECT title, remind_at AT TIME ZONE 'America/Sao_Paulo' AS remind_at, status FROM agenda.agd006_reminder WHERE title ILIKE '%pão%'"),
        new("Excluir vários", "minhas tarefas", "exclui a 10, a 11 e a 12", "!confirm"),
        new("Gasto sem dizer onde", "gastei 30 no uber"),
        new("Fatura de um cartão", "quanto está a fatura do Nubank?"),
        new("Semana vs semana", "gastei mais essa semana ou na passada?"),
        new("Lembretes que tenho", "quais lembretes eu tenho?", "cancela o 2", "!confirm"),
        new("Evento que não existe", "cancela minha reunião de amanhã"),
        new("Excluir sem confirmar", "exclui a tarefa organizar fotos", "deixa, não precisa"),
        new("Sim por texto", "exclui a tarefa organizar fotos", "sim",
            "?SELECT status, count(*) FROM assistant.ast004_command_invocation WHERE command_name = 'delete_task' GROUP BY status"),
        new("Não por texto", "cancela o lembrete de ligar pra mãe", "não, deixa",
            "?SELECT status, count(*) FROM assistant.ast004_command_invocation WHERE command_name = 'cancel_reminder' GROUP BY status"),

        // ── Off-topic ─────────────────────────────────────────────────────────────────────────────
        new("Conversa", "oi, tudo bem?"),
        new("Fora do escopo", "quanto é 15% de 230?"),
    ];

    [LiveGeminiFact]
    public async Task Talk_to_the_assistant_as_telegram_would()
    {
        var report = new StringBuilder($"# Assistant live run — {DateTimeOffset.Now:yyyy-MM-dd HH:mm}\n");
        // PANDORA_LIVE_ONLY: names (or parts of names) separated by '|' — runs just those scenarios.
        var only = Environment.GetEnvironmentVariable("PANDORA_LIVE_ONLY")?.Split('|', StringSplitOptions.RemoveEmptyEntries);

        foreach (var scenario in Scenarios.Where(s => only is null || only.Any(o => s.Name.Contains(o, StringComparison.OrdinalIgnoreCase))))
        {
            await factory.ReseedAsync();
            report.Append($"\n## {scenario.Name}\n");

            foreach (var step in scenario.Steps)
            {
                try
                {
                    if (step.StartsWith('?'))
                        report.Append(await QueryAsync(step[1..]));
                    else if (step.StartsWith('!'))
                        report.Append(await TapAsync(step[1..]));
                    else
                        report.Append(await SayAsync(step));
                }
                catch (Exception ex)
                {
                    report.Append($"\n> 💥 `{step}` threw {ex.GetType().Name}: {ex.Message}\n");
                }
            }
        }

        var path = Environment.GetEnvironmentVariable("PANDORA_LIVE_REPORT")
            ?? Path.Combine(Path.GetTempPath(), "pandora-assistant-live.md");
        await File.WriteAllTextAsync(path, report.ToString());
        output.WriteLine($"Report: {path}");
    }

    private async Task<string> SayAsync(string text)
    {
        var since = await NowAsync();
        var sentBefore = factory.Telegram.Sent.Count;
        var clock = Stopwatch.StartNew();

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var @event = new InboundMessageReceived(Guid.CreateVersion7(), DateTimeOffset.UtcNow, UserId, "telegram", "assistant", text, null, null);
            foreach (var handler in scope.ServiceProvider.GetServices<IIntegrationEventHandler<InboundMessageReceived>>())
                await handler.HandleAsync(@event);
        }
        await factory.DrainOutboxAsync("assistant", "channels");

        return $"\n**🧑 {text}**  _({clock.ElapsedMilliseconds} ms)_\n" + await InvocationsAsync(since) + Replies(sentBefore);
    }

    private async Task<string> TapAsync(string action)
    {
        var last = factory.Telegram.Sent.LastOrDefault(m => m.Buttons.Count > 0);
        var button = last.Buttons?.FirstOrDefault(b => b.Label.Contains(action == "confirm" ? "Confirm" : "Cancel"));
        if (button is null)
            return $"\n**👆 {action}** — no such button on the last reply\n";

        string? owner, kind, payload;
        await using (var connection = await factory.OpenAsync())
        await using (var cmd = new NpgsqlCommand(
            "SELECT owner_module, action, payload FROM channels.chn003_interaction WHERE id = @id", connection))
        {
            cmd.Parameters.AddWithValue("id", Guid.Parse(button.InteractionId));
            await using var reader = await cmd.ExecuteReaderAsync();
            await reader.ReadAsync();
            (owner, kind, payload) = (reader.GetString(0), reader.GetString(1), reader.IsDBNull(2) ? null : reader.GetString(2));
        }

        var sentBefore = factory.Telegram.Sent.Count;
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var @event = new InboundInteractionReceived(Guid.CreateVersion7(), DateTimeOffset.UtcNow, UserId, "telegram", owner, kind, payload);
            foreach (var handler in scope.ServiceProvider.GetServices<IIntegrationEventHandler<InboundInteractionReceived>>())
                await handler.HandleAsync(@event);
        }
        await factory.DrainOutboxAsync("assistant", "channels");

        return $"\n**👆 {button.Label}**\n" + Replies(sentBefore);
    }

    private async Task<string> QueryAsync(string sql)
    {
        await using var connection = await factory.OpenAsync();
        await using var cmd = new NpgsqlCommand(sql, connection);
        await using var reader = await cmd.ExecuteReaderAsync();
        var sb = new StringBuilder($"\n🔎 `{sql}`\n\n");
        var columns = Enumerable.Range(0, reader.FieldCount).Select(reader.GetName).ToList();
        sb.Append("| ").AppendJoin(" | ", columns).Append(" |\n|").AppendJoin("|", columns.Select(_ => "---")).Append("|\n");
        while (await reader.ReadAsync())
            sb.Append("| ").AppendJoin(" | ", columns.Select((_, i) => reader.IsDBNull(i) ? "∅" : Convert.ToString(reader.GetValue(i)))).Append(" |\n");
        return sb.ToString();
    }

    /// <summary>The invocations the step recorded: tool, arguments, status, cost.</summary>
    private async Task<string> InvocationsAsync(DateTimeOffset since)
    {
        await using var connection = await factory.OpenAsync();
        await using var cmd = new NpgsqlCommand(
            """
            SELECT coalesce(command_name, '—'), status, coalesce(arguments::text, ''), coalesce(error, ''), latency_ms, prompt_tokens, completion_tokens
              FROM assistant.ast004_command_invocation
             WHERE created_at >= @since
             ORDER BY created_at
            """, connection);
        cmd.Parameters.AddWithValue("since", since);
        await using var reader = await cmd.ExecuteReaderAsync();
        var sb = new StringBuilder();
        while (await reader.ReadAsync())
        {
            sb.Append($"- 🛠️ `{reader.GetString(0)}` **{reader.GetString(1)}** `{reader.GetString(2)}`");
            if (reader.GetString(3).Length > 0)
                sb.Append($" — error: {reader.GetString(3)}");
            sb.Append($" _(model {reader.GetInt64(4)} ms, {reader.GetInt32(5)}+{reader.GetInt32(6)} tokens)_");
            sb.Append('\n');
        }
        return sb.ToString();
    }

    private string Replies(int sentBefore)
    {
        var sb = new StringBuilder();
        foreach (var (text, buttons) in factory.Telegram.Sent.Skip(sentBefore))
        {
            sb.Append("\n> 🤖 ").Append(text.Replace("\n", "\n> ")).Append('\n');
            if (buttons.Count > 0)
                sb.Append("> [").AppendJoin("] [", buttons.Select(b => b.Label)).Append("]\n");
        }
        var count = factory.Telegram.Sent.Count - sentBefore;
        if (count == 0)
            sb.Append("\n> 🤖 _(no reply)_\n");
        else if (count > 1)
            sb.Append($"\n_({count} Telegram messages)_\n");
        return sb.ToString();
    }

    private async Task<DateTimeOffset> NowAsync()
    {
        await using var connection = await factory.OpenAsync();
        await using var cmd = new NpgsqlCommand("SELECT clock_timestamp()", connection);
        return new DateTimeOffset((DateTime)(await cmd.ExecuteScalarAsync())!); // timestamptz reads back as a UTC DateTime
    }
}
