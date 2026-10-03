using Microsoft.Extensions.Logging.Abstractions;
using Pottmayer.Pandora.Modules.Assistant.Abstractions.Files;
using Pottmayer.Pandora.Modules.Assistant.Application.Commands.Interpret;
using Pottmayer.Pandora.Modules.Assistant.Application.Dtos;
using Pottmayer.Pandora.Modules.Assistant.Application.Interpret;
using Pottmayer.Pandora.Modules.Assistant.Application.Subscribers;
using Pottmayer.Pandora.Modules.Assistant.Domain.Errors;
using Pottmayer.Pandora.Modules.Assistant.Domain.Ports.Repositories;
using Pottmayer.Pandora.Modules.Assistant.Tests.Fakes;
using Pottmayer.Pandora.Modules.Channels.Abstractions;
using Pottmayer.Pandora.Modules.Channels.Contracts;
using Pottmayer.Tars.Core.Primitives.Outcomes;
using Xunit;

namespace Pottmayer.Pandora.Modules.Assistant.Tests;

public sealed class InboundMessageReceivedHandlerTests
{
    private static readonly DateTimeOffset Now = new(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);
    private readonly FixedTimeProvider _time = new(Now);

    private readonly FakeMediaReader _media = new();
    private readonly FakeFileQueue _queue = new();

    private (InboundMessageReceivedHandler Handler, FakeSender Sender, FakeIntegrationEventBus Bus) Build(
        Result<InterpretResultDto>? response = null)
    {
        var sender = new FakeSender { Response = response ?? Result<InterpretResultDto>.Success(Dto("feito ✓")) };
        var bus = new FakeIntegrationEventBus();
        var context = new FakeDataContext();
        context.Register<IAssistantProfileRepository>(new FakeAssistantProfileRepository());
        var factory = new FakeUnitOfWorkFactory(context);
        var handler = new InboundMessageReceivedHandler(
            factory, sender, bus, _media, [_queue],
            new AssistantToolContextResolver(factory, FakeEffectiveTimeZoneResolver.With("America/Sao_Paulo")),
            _time, NullLogger<InboundMessageReceivedHandler>.Instance);
        return (handler, sender, bus);
    }

    private static InterpretResultDto Dto(string message, string? transcript = null) =>
        new(Guid.NewGuid(), message, [Invocation("executed", message)], transcript);

    private static InvocationResultDto Invocation(string status, string message) =>
        new(Guid.NewGuid(), status, "create_reminder", "{}", message);

    private static InboundMessageReceived Media(string mimeType) =>
        new(Guid.NewGuid(), Now, Guid.NewGuid(), "telegram", "assistant", Text: null, MediaRef: "file-1", MediaMimeType: mimeType);

    private static InboundMessageReceived File(
        string mimeType, string? caption, string? fileName = null, long? sizeBytes = null) =>
        new(Guid.NewGuid(), Now, Guid.NewGuid(), "telegram", "assistant", caption, "file-1", mimeType, fileName, sizeBytes);

    private static InboundMessageReceived Inbound(string bot, string? text) =>
        new(Guid.NewGuid(), Now, Guid.NewGuid(), "telegram", bot, text, MediaRef: null, MediaMimeType: null);

    [Fact]
    public async Task Interprets_an_assistant_bot_message_and_publishes_the_reply()
    {
        var (handler, sender, bus) = Build(Result<InterpretResultDto>.Success(Dto("lembrete criado ✓")));
        var inbound = Inbound("assistant", "me lembra de pagar a conta amanhã");

        await handler.HandleAsync(inbound);

        Assert.IsType<InterpretCommand>(Assert.Single(sender.Sent));
        var reply = Assert.IsType<SendAssistantReply>(Assert.Single(bus.Published));
        Assert.Equal(inbound.UserId, reply.UserId);
        Assert.Equal("assistant", reply.Bot);
        Assert.Equal("lembrete criado ✓", reply.Text);
    }

    [Fact]
    public async Task Ignores_a_message_from_another_bot()
    {
        var (handler, sender, bus) = Build();

        await handler.HandleAsync(Inbound("notifications", "oi"));

        Assert.Empty(sender.Sent);
        Assert.Empty(bus.Published);
    }

    [Fact]
    public async Task Ignores_a_message_with_no_text()
    {
        var (handler, sender, bus) = Build();

        await handler.HandleAsync(Inbound("assistant", "   "));

        Assert.Empty(sender.Sent);
        Assert.Empty(bus.Published);
    }

    [Fact]
    public async Task Does_not_reply_when_interpret_is_rejected()
    {
        var (handler, sender, bus) = Build(Result<InterpretResultDto>.Failure(AssistantErrors.NotEnabled));

        await handler.HandleAsync(Inbound("assistant", "faz algo"));

        Assert.Single(sender.Sent);
        Assert.Empty(bus.Published);
    }

    [Fact]
    public async Task Does_not_reply_when_the_interpretation_has_no_message()
    {
        var (handler, sender, bus) = Build(Result<InterpretResultDto>.Success(Dto("   ")));

        await handler.HandleAsync(Inbound("assistant", "faz algo"));

        Assert.Single(sender.Sent);
        Assert.Empty(bus.Published);
    }

    [Fact]
    public async Task A_voice_note_is_downloaded_through_its_bot_interpreted_and_echoed()
    {
        _media.Bytes = [1, 2, 3];
        var (handler, sender, bus) = Build(Result<InterpretResultDto>.Success(Dto("lembrete criado ✓", "me lembra amanhã")));

        await handler.HandleAsync(Media("audio/ogg"));

        Assert.Equal(("telegram", "assistant", "file-1"), _media.Opened);
        var input = Assert.IsType<InterpretCommand>(Assert.Single(sender.Sent)).Input;
        Assert.Null(input.Text);
        Assert.Equal("audio/ogg", input.Audio!.MimeType);
        Assert.Equal(new byte[] { 1, 2, 3 }, input.Audio.Data.ToArray());
        var reply = Assert.IsType<SendAssistantReply>(Assert.Single(bus.Published));
        Assert.Equal("🎤 “me lembra amanhã”\n\nlembrete criado ✓", reply.Text);
    }

    [Fact]
    public async Task An_oversized_voice_note_is_read_only_past_the_limit_and_left_to_the_pipeline()
    {
        _media.Bytes = new byte[InterpretCommandHandler.MaxAudioBytes + 500_000];
        var (handler, sender, _) = Build();

        await handler.HandleAsync(Media("audio/ogg"));

        var audio = Assert.IsType<InterpretCommand>(Assert.Single(sender.Sent)).Input.Audio!;
        Assert.True(audio.Data.Length > InterpretCommandHandler.MaxAudioBytes);
        Assert.True(audio.Data.Length < _media.Bytes.Length);
    }

    [Fact]
    public async Task A_held_call_gets_confirm_and_cancel_buttons_carrying_its_invocation_id()
    {
        var pending = Invocation("pending-confirmation", "Criar o lembrete?");
        var (handler, _, bus) = Build(Result<InterpretResultDto>.Success(
            new InterpretResultDto(Guid.NewGuid(), pending.Message, [pending])));

        await handler.HandleAsync(Inbound("assistant", "me lembra amanhã"));

        var reply = Assert.IsType<SendAssistantReply>(Assert.Single(bus.Published));
        Assert.Collection(reply.Buttons!,
            b => Assert.Equal(("assistant", "confirm", "✅ Confirmar", pending.InvocationId.ToString()), (b.OwnerModule, b.Action, b.Label, b.Payload)),
            b => Assert.Equal(("assistant", "cancel", "❌ Cancelar", pending.InvocationId.ToString()), (b.OwnerModule, b.Action, b.Label, b.Payload)));
    }

    [Fact]
    public async Task Buttons_are_numbered_to_match_the_lines_when_there_are_several_calls()
    {
        var executed = Invocation("executed", "Lembrete criado.");
        var pending = Invocation("pending-confirmation", "Criar a tarefa?");
        var (handler, _, bus) = Build(Result<InterpretResultDto>.Success(
            new InterpretResultDto(Guid.NewGuid(), "1. Lembrete criado.\n2. Criar a tarefa?", [executed, pending])));

        await handler.HandleAsync(Inbound("assistant", "lembra X e cria Y"));

        var buttons = Assert.IsType<SendAssistantReply>(Assert.Single(bus.Published)).Buttons!;
        Assert.Equal(["✅ Confirmar 2", "❌ Cancelar 2"], buttons.Select(b => b.Label));
        Assert.All(buttons, b => Assert.Equal(pending.InvocationId.ToString(), b.Payload));
    }

    [Fact]
    public async Task A_reply_with_nothing_to_confirm_has_no_buttons()
    {
        var (handler, _, bus) = Build();

        await handler.HandleAsync(Inbound("assistant", "me lembra amanhã"));

        Assert.Null(Assert.IsType<SendAssistantReply>(Assert.Single(bus.Published)).Buttons);
    }

    [Fact]
    public async Task A_shared_file_goes_to_the_queue_its_caption_names_without_being_interpreted()
    {
        _media.Bytes = [7, 8, 9];
        var (handler, sender, bus) = Build();

        await handler.HandleAsync(File("application/pdf", "Comprovante luz setembro", "pix.pdf"));

        Assert.Empty(sender.Sent);
        var file = Assert.Single(_queue.Received);
        Assert.Equal(("pix.pdf", "application/pdf", "Comprovante luz setembro"), (file.FileName, file.ContentType, file.Caption));
        Assert.Equal(new byte[] { 7, 8, 9 }, file.Content);
        var reply = Assert.IsType<SendAssistantReply>(Assert.Single(bus.Published));
        Assert.Equal("📎 Na fila do Finances: pix.pdf. Atribua pelo app.", reply.Text);
    }

    [Fact]
    public async Task Caption_keywords_match_without_accents_and_a_photo_gets_a_name()
    {
        var (handler, _, _) = Build();

        await handler.HandleAsync(File("image/jpeg", "Finanças!"));

        Assert.Equal("telegram-20260101-120000.jpg", Assert.Single(_queue.Received).FileName);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("olha isso")]
    public async Task A_file_whose_caption_names_no_queue_is_not_downloaded_and_the_reply_asks_where(string? caption)
    {
        var (handler, sender, bus) = Build();

        await handler.HandleAsync(File("image/jpeg", caption));

        Assert.Null(_media.Opened);
        Assert.Empty(sender.Sent);
        Assert.Empty(_queue.Received);
        var reply = Assert.IsType<SendAssistantReply>(Assert.Single(bus.Published));
        Assert.Contains("“comprovante”, “boleto”, “financeiro”", reply.Text);
    }

    [Fact]
    public async Task Only_images_and_pdfs_are_kept()
    {
        var (handler, _, bus) = Build();

        await handler.HandleAsync(File("video/mp4", "financeiro"));

        Assert.Null(_media.Opened);
        Assert.Empty(_queue.Received);
        Assert.Equal("📎 Só guardo imagens e PDFs.", Assert.IsType<SendAssistantReply>(Assert.Single(bus.Published)).Text);
    }

    [Fact]
    public async Task A_file_larger_than_a_bot_may_download_is_refused_before_downloading()
    {
        var (handler, _, bus) = Build();

        await handler.HandleAsync(File("application/pdf", "boleto", "big.pdf", SharedFile.MaxBytes + 1));

        Assert.Null(_media.Opened);
        Assert.Empty(_queue.Received);
        Assert.StartsWith("📎 Arquivo grande demais", Assert.IsType<SendAssistantReply>(Assert.Single(bus.Published)).Text);
    }

    private sealed class FakeFileQueue : IAssistantFileQueue
    {
        public List<SharedFile> Received { get; } = [];
        public string Name => "Finances";
        public IReadOnlyList<string> Keywords { get; } = ["comprovante", "boleto", "financeiro", "financas"];

        public Task<bool> EnqueueAsync(SharedFile file, CancellationToken ct = default)
        {
            Received.Add(file);
            return Task.FromResult(true);
        }
    }

    private sealed class FakeMediaReader : IInboundMediaReader
    {
        public byte[] Bytes { get; set; } = [];
        public (string Channel, string Bot, string MediaRef)? Opened { get; private set; }

        public Task<Stream> OpenAsync(string channel, string bot, string mediaRef, CancellationToken ct = default)
        {
            Opened = (channel, bot, mediaRef);
            return Task.FromResult<Stream>(new MemoryStream(Bytes));
        }
    }
}
