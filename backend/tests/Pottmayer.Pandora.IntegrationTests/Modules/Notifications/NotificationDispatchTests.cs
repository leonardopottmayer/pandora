using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using Pottmayer.Pandora.IntegrationTests.Support;
using Pottmayer.Pandora.Modules.Notifications.Application.Commands.DispatchPending;
using Pottmayer.Tars.Core.Mediator.Abstractions;
using Pottmayer.Tars.Core.Primitives.Outcomes;
using Xunit;

namespace Pottmayer.Pandora.IntegrationTests.Modules.Notifications;

/// <summary>
/// End-to-end of the notifications pipeline against a real database: signing up publishes an
/// integration event, the in-process subscriber enqueues a notification, and the dispatch command
/// drains it through the e-mail sender.
/// </summary>
[Collection("Integration")]
public sealed class NotificationDispatchTests : IAsyncLifetime
{
    private const string SignUpUrl = "/api/v1/identity/auth/signup";

    private readonly PandoraWebApplicationFactory _factory;
    private readonly HttpClient _client;
    private readonly NotificationsProbe _notifications;

    public NotificationDispatchTests(PandoraWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
        _notifications = new NotificationsProbe(factory.ConnectionString);
    }

    public Task InitializeAsync() => _factory.ResetDatabaseAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task SignUp_enqueues_a_pending_activation_notification()
    {
        var email = "alice@example.com";

        var response = await SignUpAsync(email, "alice");

        Assert.True(response.IsSuccessStatusCode, await response.Content.ReadAsStringAsync());

        var row = await _notifications.WaitForRecipientAsync(email);
        Assert.Equal("account-activation", row.TemplateKey);
        Assert.Equal("Pending", row.Status);
        Assert.Equal(0, row.AttemptCount);
        Assert.Null(row.Provider);
    }

    [Fact]
    public async Task Dispatch_marks_the_pending_notification_as_sent()
    {
        var email = "bob@example.com";
        await SignUpAsync(email, "bob");
        await _notifications.WaitForRecipientAsync(email);

        var result = await DispatchAsync();

        Assert.True(result.IsSuccess);
        Assert.True(result.Value!.Sent >= 1);

        var row = await _notifications.FindByRecipientAsync(email);
        Assert.NotNull(row);
        Assert.Equal("Sent", row!.Status);
        Assert.False(string.IsNullOrWhiteSpace(row.Provider));
    }

    [Fact]
    public async Task SignUp_with_a_duplicate_email_enqueues_no_extra_notification()
    {
        var email = "carol@example.com";

        var first = await SignUpAsync(email, "carol");
        Assert.True(first.IsSuccessStatusCode);
        await _notifications.WaitForRecipientAsync(email);

        // Same e-mail, different username: the second sign-up must be rejected.
        var second = await SignUpAsync(email, "carol2");
        Assert.False(second.IsSuccessStatusCode);

        Assert.Equal(1, await _notifications.CountAsync());
    }

    private Task<HttpResponseMessage> SignUpAsync(string email, string username)
        => _client.PostAsJsonAsync(SignUpUrl, new
        {
            name = "Test User",
            username,
            email,
            password = "correct horse battery staple"
        });

    private async Task<Result<DispatchPendingNotificationsResult>> DispatchAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var sender = scope.ServiceProvider.GetRequiredService<ISender>();
        return await sender.Send(
            new DispatchPendingNotificationsCommand(new DispatchPendingNotificationsInput(BatchSize: 50)),
            CancellationToken.None);
    }
}
