using System.Net;
using System.Net.Http.Json;
using Pottmayer.Pandora.IntegrationTests.Support;
using Pottmayer.Pandora.Modules.Files.Agent;
using Xunit;

namespace Pottmayer.Pandora.IntegrationTests.Modules.Files;

/// <summary>
/// The Files catalog end to end: the user configures roots, selection and filters; a paired device's
/// agent pulls the config, walks a (simulated) disk with the shared rules, and reports through the scan
/// protocol. Nothing leaves the catalog without the user's review.
/// </summary>
[Collection("Integration")]
public sealed class FilesScanTests : IAsyncLifetime
{
    private const string Files = "/api/v1/files";
    private const string Agent = "/api/v1/files/agent";

    private readonly PandoraWebApplicationFactory _factory;
    private readonly HttpClient _user;
    private readonly HttpClient _device;

    public FilesScanTests(PandoraWebApplicationFactory factory)
    {
        _factory = factory;
        _user = factory.CreateClient();
        _device = factory.CreateClient();
    }

    public Task InitializeAsync() => _factory.ResetDatabaseAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task A_scan_catalogs_exactly_the_selection_and_a_renamed_file_keeps_its_id()
    {
        var deviceId = await SetUpAsync();
        var root = await AddRootAsync(deviceId);
        await PutAsync($"{Files}/roots/{root.Id}/selection", new { marks = new[] { new { path = "/C", mode = "exclude" }, new { path = "/C/Sub", mode = "include" } } });
        await PostAsync<object>($"{Files}/filters", new { rootId = root.Id, name = "Videos", action = "include", appliesTo = "file", matcher = "extension", pattern = "mkv, mp4" });

        var disk = new Disk()
            .Dir("/A").File("/A/movie.mkv", "movie").File("/A/notes.txt", "notes").File("/A/Thumbs.db", "thumbs")
            .Dir("/C").File("/C/c.mkv", "c").Dir("/C/Sub").File("/C/Sub/Breaking Bad s02e01.mkv", "bb");

        var first = await ScanAsync(root.Id, disk);
        Assert.Equal("completed", first.Status);
        Assert.Equal(5, first.Created); // /A, /A/movie.mkv, /C, /C/Sub, /C/Sub/…s02e01.mkv

        var found = Assert.Single(await SearchAsync("bad S02"));
        Assert.Equal("/C/Sub/Breaking Bad s02e01.mkv", found.RelativePath);
        Assert.Empty(await SearchAsync("notes"));
        var movie = Assert.Single(await SearchAsync("movie"));

        // Renamed on disk: same bytes, new name.
        disk.Rename("/A/movie.mkv", "/A/film.mkv");
        var second = await ScanAsync(root.Id, disk);

        Assert.Equal("completed", second.Status);
        Assert.Equal(1, second.Moved);
        Assert.Equal(0, second.Missing);
        var film = Assert.Single(await SearchAsync("film"));
        Assert.Equal(movie.Id, film.Id);
        Assert.Empty(await SearchAsync("movie", status: "all"));
    }

    [Fact]
    public async Task A_disk_that_comes_up_mostly_empty_is_held_and_missing_entries_wait_for_review()
    {
        var deviceId = await SetUpAsync();
        var root = await AddRootAsync(deviceId);

        var disk = new Disk().Dir("/D");
        for (var i = 0; i < 9; i++) disk.File($"/D/f{i}.txt", $"content {i}");
        Assert.Equal("completed", (await ScanAsync(root.Id, disk)).Status);

        // Eight of ten entries vanish: the brake holds the scan and nothing is marked yet.
        for (var i = 1; i < 9; i++) disk.Remove($"/D/f{i}.txt");
        var held = await ScanAsync(root.Id, disk);
        Assert.Equal("held", held.Status);
        Assert.Equal(8, held.Missing);
        Assert.Empty(await SearchAsync("f", status: "missing"));

        var confirmed = await PostAsync<ScanData>($"{Files}/scans/{held.Id}/confirm", new { });
        Assert.Equal("completed", confirmed.Status);
        Assert.Equal(8, (await SearchAsync("f", status: "missing")).Count);

        // The inbox, as a tree: the root, then /D holding all eight.
        var top = Assert.Single(await GetAsync<List<ReviewNodeData>>($"{Files}/review/tree"));
        Assert.Equal(8, top.Count);
        var d = Assert.Single(await GetAsync<List<ReviewNodeData>>($"{Files}/review/tree?rootId={root.Id}"));
        Assert.Equal(("/D", 8, true), (d.Path, d.Count, d.HasChildren));
        var leaves = await GetAsync<List<ReviewNodeData>>($"{Files}/review/tree?rootId={root.Id}&parentPath=/D");
        Assert.Equal(8, leaves.Count);

        // Keep one, forget the rest of the folder.
        var keep = leaves.Single(l => l.Name == "f1.txt");
        Assert.Equal(1, await PostAsync<int>($"{Files}/review/keep", new { entryIds = new[] { keep.EntryId } }));
        Assert.Equal(7, await PostAsync<int>($"{Files}/review/forget", new { folders = new[] { new { rootId = root.Id, path = "/D" } } }));

        Assert.Empty(await GetAsync<List<ReviewNodeData>>($"{Files}/review/tree"));
        var all = await SearchAsync("", status: "all");
        Assert.Equal(["/D", "/D/f0.txt", "/D/f1.txt"], all.Select(e => e.RelativePath).Order());
    }

    [Fact]
    public async Task What_a_config_change_leaves_out_is_excluded_not_missing()
    {
        var deviceId = await SetUpAsync();
        var root = await AddRootAsync(deviceId);
        var disk = new Disk().Dir("/A").File("/A/x.txt", "x").Dir("/B").File("/B/y.txt", "y");
        await ScanAsync(root.Id, disk);

        await PutAsync($"{Files}/roots/{root.Id}/selection", new { marks = new[] { new { path = "/B", mode = "exclude" } } });
        var scan = await ScanAsync(root.Id, disk);

        // Half the root left the selection: the user asked for it, so the brake does not fire.
        Assert.Equal(("completed", 0, 2), (scan.Status, scan.Missing, scan.Excluded));
        Assert.Equal("excluded", Assert.Single(await SearchAsync("y.txt", status: "all")).Status);
    }

    [Fact]
    public async Task A_lost_batch_aborts_the_scan_instead_of_marking_files_missing()
    {
        var deviceId = await SetUpAsync();
        var root = await AddRootAsync(deviceId);
        await ScanAsync(root.Id, new Disk().File("/a.txt", "a").File("/b.txt", "b"));

        var scanId = (await AgentPostAsync<StartScanResponse>($"{Agent}/scans", new StartScanRequest(root.Id))).ScanId;
        var done = await AgentPostAsync<ScanData>($"{Agent}/scans/{scanId}/complete", new CompleteScanRequest(2));

        Assert.Equal(("aborted", "entries-seen-mismatch"), (done.Status, done.Error));
        Assert.Equal(2, (await SearchAsync("txt")).Count);
    }

    [Fact]
    public async Task The_agent_api_takes_only_a_device_key_and_only_for_its_own_roots()
    {
        var deviceId = await SetUpAsync();
        var root = await AddRootAsync(deviceId);

        // A session is not a device.
        Assert.Equal(HttpStatusCode.Unauthorized, (await _user.GetAsync($"{Agent}/config")).StatusCode);

        // Another device of the same user sees none of this device's roots.
        var other = _factory.CreateClient();
        other.DefaultRequestHeaders.Add("X-Api-Key", (await PairAsync("NOTEBOOK")).Key);
        Assert.Empty((await other.GetFromJsonAsync<Envelope<AgentConfig>>($"{Agent}/config"))!.Data.Roots);
        var start = await other.PostAsJsonAsync($"{Agent}/scans", new StartScanRequest(root.Id));
        Assert.Equal(HttpStatusCode.NotFound, start.StatusCode);

        // With the account switch off, the agent has nothing to do.
        await PutAsync($"{Files}/preferences", new { isEnabled = false });
        var config = (await _device.GetFromJsonAsync<Envelope<AgentConfig>>($"{Agent}/config"))!.Data;
        Assert.False(config.Enabled);
        Assert.Equal(HttpStatusCode.Forbidden, (await _device.PostAsJsonAsync($"{Agent}/scans", new StartScanRequest(root.Id))).StatusCode);
    }

    [Fact]
    public async Task A_draft_filter_is_previewed_against_the_catalog()
    {
        var deviceId = await SetUpAsync();
        var root = await AddRootAsync(deviceId);
        await ScanAsync(root.Id, new Disk().File("/a.mkv", "a").File("/b.MKV", "b").File("/c.srt", "c"));

        var preview = await PostAsync<PreviewData>($"{Files}/filters/preview",
            new { rootId = root.Id, name = "Videos", action = "include", appliesTo = "file", matcher = "regex", pattern = @"\.mkv$" });

        Assert.Equal(2, preview.Count); // the Windows root ignores case by default

        var invalid = await _user.PostAsJsonAsync($"{Files}/filters/preview",
            new { name = "Broken", action = "exclude", appliesTo = "file", matcher = "regex", pattern = "(" });
        Assert.Equal(HttpStatusCode.UnprocessableEntity, invalid.StatusCode);
    }

    // ── The agent, simulated ──

    /// <summary>Pulls the config, walks the disk through the shared rules, and runs the scan protocol.</summary>
    private async Task<ScanData> ScanAsync(Guid rootId, Disk disk)
    {
        var config = (await _device.GetFromJsonAsync<Envelope<AgentConfig>>($"{Agent}/config"))!.Data;
        var rootConfig = config.Roots.Single(r => r.Id == rootId);
        var rules = new CatalogRules(rootConfig.CaseSensitive, rootConfig.Marks, rootConfig.Filters);
        var walked = disk.Items.Where(i => rules.Includes(i.Path, i.Kind)).ToList();

        var scanId = (await AgentPostAsync<StartScanResponse>($"{Agent}/scans", new StartScanRequest(rootId))).ScanId;

        var first = await AgentPostAsync<ScanBatchResult>($"{Agent}/scans/{scanId}/batches",
            new ScanBatch([.. walked.Select(i => i.ToEntry(withFingerprint: false))]));
        if (first.NeedsFingerprint.Count > 0)
            await AgentPostAsync<ScanBatchResult>($"{Agent}/scans/{scanId}/batches",
                new ScanBatch([.. walked.Where(i => first.NeedsFingerprint.Contains(i.Path)).Select(i => i.ToEntry(withFingerprint: true))]));

        return await AgentPostAsync<ScanData>($"{Agent}/scans/{scanId}/complete", new CompleteScanRequest(walked.Count));
    }

    private sealed class Disk
    {
        private static readonly DateTimeOffset Modified = new(2026, 1, 1, 10, 0, 0, TimeSpan.Zero);
        public List<DiskItem> Items { get; } = [];

        public Disk Dir(string path) { Items.Add(new DiskItem(path, AgentValues.Directory, "")); return this; }
        public Disk File(string path, string content) { Items.Add(new DiskItem(path, AgentValues.File, content)); return this; }
        public void Remove(string path) => Items.RemoveAll(i => i.Path == path);

        public void Rename(string from, string to)
        {
            var item = Items.Single(i => i.Path == from);
            Items[Items.IndexOf(item)] = item with { Path = to };
        }

        public sealed record DiskItem(string Path, string Kind, string Content)
        {
            public ScannedEntry ToEntry(bool withFingerprint)
            {
                if (Kind == AgentValues.Directory) return new ScannedEntry(Path, Kind, 0, null, null);
                var bytes = System.Text.Encoding.UTF8.GetBytes(Content);
                var fingerprint = withFingerprint ? Fingerprint.ComputeAsync(new MemoryStream(bytes)).GetAwaiter().GetResult() : null;
                return new ScannedEntry(Path, Kind, bytes.Length, Modified, fingerprint);
            }
        }
    }

    // ── Setup and HTTP ──

    /// <summary>Signs in, turns Files on and pairs the device; returns its id.</summary>
    private async Task<Guid> SetUpAsync()
    {
        await IdentityHelper.AuthenticateAsync(_user, _factory.ConnectionString, "alice@example.com", "alice");
        await PutAsync($"{Files}/preferences", new { isEnabled = true });
        var paired = await PairAsync("HOMELAB");
        _device.DefaultRequestHeaders.Add("X-Api-Key", paired.Key);
        return paired.Device.Id;
    }

    private async Task<Registration> PairAsync(string name) =>
        await PostAsync<Registration>("/api/v1/identity/devices", new { name, platform = "windows", form = "desktop", scopes = Array.Empty<string>() });

    private Task<RootData> AddRootAsync(Guid deviceId) =>
        PostAsync<RootData>($"{Files}/roots", new { deviceId, name = "Disk 2", localPath = @"E:\", scanTime = "03:00:00" });

    private async Task<List<EntryData>> SearchAsync(string q, string? status = null) =>
        (await GetAsync<PageData<EntryData>>($"{Files}/search?q={Uri.EscapeDataString(q)}&take=200{(status is null ? "" : $"&status={status}")}")).Items;

    private async Task<T> GetAsync<T>(string url)
    {
        var response = await _user.GetAsync(url);
        Assert.True(response.IsSuccessStatusCode, $"{url}: {await response.Content.ReadAsStringAsync()}");
        return (await response.Content.ReadFromJsonAsync<Envelope<T>>())!.Data;
    }

    private async Task<T> PostAsync<T>(string url, object body) => await ReadAsync<T>(await _user.PostAsJsonAsync(url, body), url);

    private async Task<T> AgentPostAsync<T>(string url, object body) => await ReadAsync<T>(await _device.PostAsJsonAsync(url, body), url);

    private async Task PutAsync(string url, object body)
    {
        var response = await _user.PutAsJsonAsync(url, body);
        Assert.True(response.IsSuccessStatusCode, $"{url}: {await response.Content.ReadAsStringAsync()}");
    }

    private static async Task<T> ReadAsync<T>(HttpResponseMessage response, string url)
    {
        Assert.True(response.IsSuccessStatusCode, $"{url}: {await response.Content.ReadAsStringAsync()}");
        return (await response.Content.ReadFromJsonAsync<Envelope<T>>())!.Data;
    }

    private sealed record Envelope<T>(T Data);
    private sealed record Registration(DeviceData Device, string Key);
    private sealed record DeviceData(Guid Id);
    private sealed record RootData(Guid Id);
    private sealed record PageData<T>(List<T> Items, bool HasMore);
    private sealed record EntryData(Guid Id, string RelativePath, string Status);
    private sealed record ScanData(Guid Id, string Status, int Created, int Moved, int Missing, int Excluded, string? Error);
    private sealed record ReviewNodeData(string Name, string Path, int Count, bool HasChildren, Guid? EntryId);
    private sealed record PreviewData(int Count);
}
