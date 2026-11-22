using System.Text.Json;
using Pottmayer.Pandora.Modules.Assistant.Abstractions.Commands;

namespace Pottmayer.Pandora.Modules.Assistant.Tests.Fakes;

/// <summary>
/// A stand-in tool a test registers in the catalog. It records the arguments it was called with and
/// returns (or throws) whatever the test configured, so the pipeline's branches can be exercised without
/// a real module.
/// </summary>
internal sealed class FakeAssistantTool : IAssistantTool
{
    private readonly Func<JsonElement, AssistantCommandOutcome> _behavior;

    private FakeAssistantTool(
        string name, Func<JsonElement, AssistantCommandOutcome> behavior,
        ConfirmationPolicy policy = ConfirmationPolicy.WhenAmbiguous)
    {
        Descriptor = new AssistantCommandDescriptor(
            name, $"Fake tool {name}.", """{ "type": "object" }""",
            policy, []);
        _behavior = behavior;
    }

    public static FakeAssistantTool Succeeds(string name, string message = "done") =>
        new(name, _ => AssistantCommandOutcome.Ok(message));

    public static FakeAssistantTool Reads(string name, string message, string recap) =>
        new(name, _ => AssistantCommandOutcome.Ok(message, recap));

    public static FakeAssistantTool Lists(string name, string message, string recap, params ListedItem[] listed) =>
        new(name, _ => AssistantCommandOutcome.Ok(message, recap, listed));

    /// <summary>A tool whose calls must always be confirmed, like a deletion.</summary>
    public static FakeAssistantTool Required(string name) =>
        new(name, _ => AssistantCommandOutcome.Ok("done"), ConfirmationPolicy.Required);

    public static FakeAssistantTool Fails(string name, string message = "nope") =>
        new(name, _ => AssistantCommandOutcome.Failed(message));

    public static FakeAssistantTool Throws(string name, Exception ex) =>
        new(name, _ => throw ex);

    public AssistantCommandDescriptor Descriptor { get; }
    public JsonElement? LastArguments { get; private set; }
    public AssistantToolContext? LastContext { get; private set; }
    public int Calls { get; private set; }

    public string Describe(AssistantToolContext context, JsonElement arguments) => $"Run {Descriptor.Name}?";

    public Task<AssistantCommandOutcome> ExecuteAsync(AssistantToolContext context, JsonElement arguments, CancellationToken ct = default)
    {
        LastContext = context;
        LastArguments = arguments.Clone(); // detach from a caller's JsonDocument lifetime
        Calls++;
        return Task.FromResult(_behavior(arguments));
    }
}
