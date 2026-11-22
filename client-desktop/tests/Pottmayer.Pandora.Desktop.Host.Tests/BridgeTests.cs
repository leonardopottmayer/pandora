using System.Text.Json;
using Pottmayer.Pandora.Desktop.Host;
using Xunit;

namespace Pottmayer.Pandora.Desktop.Host.Tests;

public class BridgeTests
{
    private static readonly Uri Server = new("http://192.168.1.10:8730/");

    private static Bridge NewBridge() => new([
        new DelegateBridgeHandler("desktop.echo", args => args?.GetProperty("value").GetString()),
        new DelegateBridgeHandler("desktop.fail", _ => throw new InvalidOperationException("boom")),
    ]);

    private static JsonElement Parse(string? json) => JsonDocument.Parse(json!).RootElement;

    [Fact]
    public async Task Answers_a_call_from_the_server_origin()
    {
        var reply = await NewBridge().HandleAsync(Server, "http://192.168.1.10:8730/settings",
            """{"id":7,"method":"desktop.echo","args":{"value":"hi"}}""", CancellationToken.None);

        var root = Parse(reply);
        Assert.Equal(7, root.GetProperty("id").GetInt64());
        Assert.Equal("hi", root.GetProperty("result").GetString());
    }

    [Theory]
    [InlineData("https://evil.example/")]
    [InlineData("http://192.168.1.10:9999/")] // same host, another port
    [InlineData("https://192.168.1.10:8730/")] // same host and port, another scheme
    [InlineData("https://pandora-desktop.local/setup.html")]
    [InlineData(null)]
    public async Task Drops_calls_from_any_other_origin(string? source)
    {
        var reply = await NewBridge().HandleAsync(Server, source,
            """{"id":1,"method":"desktop.echo","args":{"value":"hi"}}""", CancellationToken.None);

        Assert.Null(reply);
    }

    [Fact]
    public async Task Drops_everything_while_no_server_is_configured()
    {
        var reply = await NewBridge().HandleAsync(null, "http://192.168.1.10:8730/",
            """{"id":1,"method":"desktop.echo"}""", CancellationToken.None);

        Assert.Null(reply);
    }

    [Fact]
    public async Task Unknown_method_and_handler_exception_become_errors()
    {
        var bridge = NewBridge();

        var unknown = Parse(await bridge.HandleAsync(Server, Server.ToString(), """{"id":1,"method":"files.nope"}""", CancellationToken.None));
        var failed = Parse(await bridge.HandleAsync(Server, Server.ToString(), """{"id":2,"method":"desktop.fail"}""", CancellationToken.None));

        Assert.Contains("files.nope", unknown.GetProperty("error").GetString());
        Assert.Equal("boom", failed.GetProperty("error").GetString());
    }

    [Fact]
    public void Duplicate_method_names_fail_at_startup()
    {
        Assert.Throws<ArgumentException>(() => new Bridge([
            new DelegateBridgeHandler("desktop.x", _ => null),
            new DelegateBridgeHandler("desktop.x", _ => null),
        ]));
    }

    [Theory]
    [InlineData("192.168.1.10:8730", "http://192.168.1.10:8730/")]
    [InlineData("  https://pandora.example.com  ", "https://pandora.example.com/")]
    [InlineData("http://localhost:5173/", "http://localhost:5173/")]
    public void Parses_what_the_user_types_as_the_server(string input, string expected) =>
        Assert.Equal(expected, ShellUrls.TryParseServer(input)?.ToString());

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("ftp://nas.local")]
    [InlineData("file:///C:/Windows")]
    [InlineData("javascript:alert(1)")]
    public void Rejects_anything_but_http_and_https(string input) =>
        Assert.Null(ShellUrls.TryParseServer(input));
}
