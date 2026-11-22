using Pottmayer.Pandora.Modules.Assistant.Domain.ValueObjects;
using Pottmayer.Pandora.Shared.Domain;
using Pottmayer.Tars.Core.Ddd;

namespace Pottmayer.Pandora.Modules.Assistant.Domain.Aggregates;

/// <summary>
/// One user's assistant configuration: which chat provider, account and model interpret their language,
/// whether the assistant is on, and how readily it executes without confirming. The API key itself lives
/// in the Integrations module — this aggregate only holds the id of the account the user picked
/// (<see cref="ChatAccountId"/>), and the key is fetched from there per call, so it never holds a secret.
/// </summary>
public sealed class AssistantProfile : AggregateRoot<Guid>, IAuditable
{
    public Guid UserId { get; private set; }

    /// <summary>The chat provider key, matching the Integrations account and the AI client factory (e.g. <c>gemini</c>).</summary>
    public string ChatProvider { get; private set; } = null!;

    /// <summary>
    /// The Integrations account whose API key the chat uses. Null until the user picks one; a user with
    /// several keys at the provider decides which one the assistant spends.
    /// </summary>
    public Guid? ChatAccountId { get; private set; }

    /// <summary>The model the provider should use (e.g. a fast Gemini model). Chosen per call from here.</summary>
    public string ChatModel { get; private set; } = null!;

    public bool IsEnabled { get; private set; }

    /// <summary>Overrides the user's Identity locale for the assistant only. Null uses the account locale.</summary>
    public string? LocaleOverride { get; private set; }

    public ConfirmationLevel ConfirmationLevel { get; private set; } = null!;

    public Guid? CreatedBy { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public Guid? UpdatedBy { get; set; }
    public DateTimeOffset? UpdatedAt { get; set; }

    private AssistantProfile() { }

    /// <summary>Creates a user's first assistant profile.</summary>
    public static AssistantProfile Create(
        Guid userId,
        string chatProvider,
        Guid? chatAccountId,
        string chatModel,
        bool isEnabled,
        string? localeOverride,
        ConfirmationLevel confirmationLevel,
        TimeProvider timeProvider) =>
        new()
        {
            Id = Guid.CreateVersion7(),
            UserId = userId,
            ChatProvider = chatProvider,
            ChatAccountId = chatAccountId,
            ChatModel = chatModel,
            IsEnabled = isEnabled,
            LocaleOverride = localeOverride,
            ConfirmationLevel = confirmationLevel,
            CreatedAt = timeProvider.GetUtcNow()
        };

    /// <summary>Replaces the mutable configuration from a settings save.</summary>
    public void Update(
        string chatProvider,
        Guid? chatAccountId,
        string chatModel,
        bool isEnabled,
        string? localeOverride,
        ConfirmationLevel confirmationLevel)
    {
        ChatProvider = chatProvider;
        ChatAccountId = chatAccountId;
        ChatModel = chatModel;
        IsEnabled = isEnabled;
        LocaleOverride = localeOverride;
        ConfirmationLevel = confirmationLevel;
    }
}
