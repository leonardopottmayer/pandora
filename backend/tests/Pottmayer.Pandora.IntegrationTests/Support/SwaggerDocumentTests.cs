using Microsoft.Extensions.DependencyInjection;
using Swashbuckle.AspNetCore.Swagger;
using Xunit;

namespace Pottmayer.Pandora.IntegrationTests.Support;

/// <summary>
/// Generating the document fails on the whole API when two modules name a request the same, and only a request
/// for it shows that: the rest of the API keeps working.
/// </summary>
[Collection("Integration")]
public sealed class SwaggerDocumentTests(PandoraWebApplicationFactory factory)
{
    [Fact]
    public void The_document_generates_for_every_module()
    {
        using var scope = factory.Services.CreateScope();

        var document = scope.ServiceProvider.GetRequiredService<ISwaggerProvider>().GetSwagger("v1");

        Assert.Contains(document.Paths.Keys, path => path.Contains("agenda/preferences", StringComparison.Ordinal));
    }
}
