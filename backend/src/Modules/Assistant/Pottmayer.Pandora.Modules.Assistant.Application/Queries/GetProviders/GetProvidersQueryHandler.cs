using Pottmayer.Pandora.Modules.Assistant.Application.Dtos;
using Pottmayer.Pandora.Modules.Integrations.Abstractions.Ports;
using Pottmayer.Tars.Core.Cqrs.Queries;
using Pottmayer.Tars.Core.Primitives.Outcomes;

namespace Pottmayer.Pandora.Modules.Assistant.Application.Queries.GetProviders;

/// <summary>
/// The chat providers the assistant can use, cross-referenced with the API keys the user has stored in
/// Integrations, so the settings screen can show which are ready. Cheap by design: it reads account
/// summaries (no secret, no external call). The live round-trip is a separate reachability test.
/// </summary>
public sealed class GetProvidersQueryHandler(IExternalAccountReader accountReader)
    : QueryHandlerBase<GetProvidersQuery, IReadOnlyList<AssistantProviderDto>>
{
    // The providers the assistant has an Ai.Chat client for. Only Gemini today; OpenAI is one entry more.
    private static readonly (string Provider, string DisplayName)[] Catalog =
    [
        (AssistantDefaults.Provider, "Google Gemini"),
    ];

    protected override async Task<Result<IReadOnlyList<AssistantProviderDto>>> HandleAsync(
        GetProvidersQuery request, CancellationToken cancellationToken)
    {
        var accounts = await accountReader.ListAsync(request.Input.UserId, cancellationToken);
        var byProvider = accounts.ToDictionary(a => a.Provider, StringComparer.OrdinalIgnoreCase);

        var result = new List<AssistantProviderDto>(Catalog.Length);
        foreach (var (provider, displayName) in Catalog)
        {
            byProvider.TryGetValue(provider, out var account);
            result.Add(new AssistantProviderDto(provider, displayName, account is not null, account?.DisplayName));
        }

        return Ok((IReadOnlyList<AssistantProviderDto>)result);
    }
}
