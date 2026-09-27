using Microsoft.Extensions.Logging.Abstractions;
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

    private (InboundMessageReceivedHandler Handler, FakeSender Sender, FakeIntegrationEventBus Bus) Build(
        Result<InterpretResultDto>? response = null)
    {
        var sender = new FakeSender { Response = response ?? Result<InterpretResultDto>.Success(Dto("feito ✓")) };
        var bus = new FakeIntegrationEventBus();
        var context = new FakeDataContext();
        context.Register<IAssistantProfileRepository>(new FakeAssistantProfileRepository());
        var factory = new FakeUnitOfWorkFactory(context);
        var handler = new InboundMessageReceivedHandler(
            factory, sender, bus, _media,
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
    public async Task Ignores_non_audio_media_without_text()
    {
        var (handler, sender, bus) = Build();

        await handler.HandleAsync(Media("image/jpeg"));

        Assert.Null(_media.Opened);
        Assert.Empty(sender.Sent);
        Assert.Empty(bus.Published);
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
