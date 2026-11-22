using Pottmayer.Pandora.Modules.Assistant.Abstractions.Text;
using Pottmayer.Pandora.Modules.Notes.Application.Commands.GenerateText;
using Pottmayer.Pandora.Modules.Notes.Tests.Fakes;
using Pottmayer.Tars.Core.Primitives.Outcomes;
using Xunit;

namespace Pottmayer.Pandora.Modules.Notes.Tests;

public sealed class GenerateTextTests
{
    [Fact]
    public async Task Generation_sends_only_the_prompt_and_stamps_server_time()
    {
        var completion = new FakeCompletion();
        var now = DateTimeOffset.Parse("2026-11-07T18:00:00Z");
        var user = Guid.NewGuid();
        var result = await new GenerateTextCommandHandler(completion, new FixedTimeProvider(now)).Handle(
            new GenerateTextCommand(new GenerateTextInput(user, " Write a checklist ")), default);
        Assert.True(result.IsSuccess);
        Assert.Equal("Generated", result.Value!.Text);
        Assert.Equal(now, result.Value.GeneratedAt);
        Assert.Equal(user, completion.UserId);
        Assert.Equal("Write a checklist", completion.Prompt);
        Assert.Contains("Do not call tools", completion.Instruction);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    public async Task Invalid_prompts_do_not_reach_the_provider(string? prompt)
    {
        var completion = new FakeCompletion();
        var handler = new GenerateTextCommandHandler(completion, TimeProvider.System);
        foreach (var invalid in new[] { prompt, new string('x', 8001) })
        {
            var result = await handler.Handle(new GenerateTextCommand(new GenerateTextInput(Guid.NewGuid(), invalid!)), default);
            Assert.Equal("Notes.InvalidPrompt", Assert.Single(result.Errors).Code);
        }
        Assert.Null(completion.Prompt);
    }

    [Fact]
    public async Task Provider_failures_preserve_the_friendly_error()
    {
        var completion = new FakeCompletion { Result = Result<string>.Failure(Error.Validation("Assistant.NotEnabled", "Enable it")) };
        var result = await new GenerateTextCommandHandler(completion, TimeProvider.System).Handle(
            new GenerateTextCommand(new GenerateTextInput(Guid.NewGuid(), "Prompt")), default);
        Assert.Equal("Assistant.NotEnabled", Assert.Single(result.Errors).Code);
    }

    private sealed class FakeCompletion : ITextCompletion
    {
        public Guid UserId { get; private set; }
        public string? Instruction { get; private set; }
        public string? Prompt { get; private set; }
        public Result<string> Result { get; init; } = Result<string>.Success("Generated");

        public Task<Result<string>> CompleteAsync(Guid userId, string instruction, string text, CancellationToken ct = default)
        {
            (UserId, Instruction, Prompt) = (userId, instruction, text);
            return Task.FromResult(Result);
        }
    }
}
