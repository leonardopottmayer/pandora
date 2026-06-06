using Xunit;

namespace Pottmayer.Pandora.IntegrationTests.Support;

/// <summary>
/// Shares a single <see cref="PandoraWebApplicationFactory"/> (and its PostgreSQL container) across
/// every integration test, instead of spinning up a container per test class.
/// </summary>
[CollectionDefinition("Integration")]
public sealed class IntegrationCollection : ICollectionFixture<PandoraWebApplicationFactory>;
