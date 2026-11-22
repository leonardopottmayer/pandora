using Microsoft.Extensions.DependencyInjection;
using Pottmayer.Pandora.IntegrationTests.Support;
using Pottmayer.Pandora.Modules.Communications.Domain.Ports;
using Pottmayer.Pandora.Modules.Communications.Domain.ValueObjects;
using Xunit;

namespace Pottmayer.Pandora.IntegrationTests.Modules.Communications;

[Collection("Integration")]
public sealed class ProviderRegistrationTests(PandoraWebApplicationFactory factory)
{
    [Fact]
    public void Every_mail_provider_has_an_adapter()
    {
        using var scope = factory.Services.CreateScope();

        foreach (var provider in MailProvider.All)
            Assert.NotNull(scope.ServiceProvider.GetRequiredKeyedService<IMailboxProvider>(provider.Value));

        Assert.True(scope.ServiceProvider.GetRequiredKeyedService<IMailboxProvider>(MailProvider.Gmail.Value).Capabilities.MultipleLabels);
        Assert.False(scope.ServiceProvider.GetRequiredKeyedService<IMailboxProvider>(MailProvider.Titan.Value).Capabilities.MultipleLabels);
    }
}
