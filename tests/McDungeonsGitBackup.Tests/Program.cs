using System.Net;
using System.Text;
using McDungeonsGitBackup.Core;

var tests = new (string Name, Func<Task> Run)[]
{
    ("SaveLocator finds profile folders with Characters/*.dat", TestSaveLocator),
    ("ArchiveService creates manifest and validates zip", TestArchiveCreateAndValidate),
    ("ArchiveService restores files and creates pre-restore backup", TestRestoreCreatesPreRestoreBackup),
    ("CharacterFile finds dat files and creates safe branch names", TestCharacterFiles),
    ("GitHubContentClient creates new content when remote file is missing", TestGitHubCreate),
    ("GitHubContentClient updates content when remote file exists", TestGitHubUpdate),
    ("GitHubContentClient creates character branch before upload", TestGitHubCharacterBranchCreate),
    ("GitHubContentClient lists branches and downloads from selected branch", TestGitHubBranchListAndDownload),
};

var failures = 0;
foreach (var test in tests)
{
    try
    {
        await test.Run();
        Console.WriteLine($"PASS {test.Name}");
    }
    catch (Exception ex)
    {
        failures++;
        Console.WriteLine($"FAIL {test.Name}");
        Console.WriteLine(ex);
    }
}

if (failures > 0)
{
    Environment.ExitCode = 1;
}

static async Task TestSaveLocator()
{
    using var temp = new TempDirectory();
    var profile = Path.Combine(temp.Path, "Account", "ProfileOne");
    var characters = Path.Combine(profile, "Characters");
    Directory.CreateDirectory(characters);
    await File.WriteAllTextAsync(Path.Combine(characters, "hero.dat"), "hero");

    var profiles = new SaveLocator().FindProfiles([temp.Path]);

    AssertEqual(1, profiles.Count);
    AssertEqual(profile, profiles[0].Path);
    AssertEqual(1, profiles[0].CharacterCount);
}

static async Task TestArchiveCreateAndValidate()
{
    using var temp = new TempDirectory();
    var source = Path.Combine(temp.Path, "source");
    Directory.CreateDirectory(Path.Combine(source, "Characters"));
    await File.WriteAllTextAsync(Path.Combine(source, "Characters", "hero.dat"), "hero-data");
    await File.WriteAllTextAsync(Path.Combine(source, "profile.json"), "{\"ok\":true}");

    var archivePath = Path.Combine(temp.Path, "backup.zip");
    var service = new ArchiveService();
    var created = await service.CreateBackupArchiveAsync(source, archivePath);
    var validated = await service.ValidateArchiveAsync(archivePath);

    AssertEqual(2, created.Files.Count);
    AssertEqual(2, validated.Files.Count);
    Assert(File.Exists(archivePath), "archive should exist");
    Assert(validated.Files.Any(file => file.RelativePath == "Characters/hero.dat"), "manifest should include hero.dat");
}

static async Task TestRestoreCreatesPreRestoreBackup()
{
    using var temp = new TempDirectory();
    var source = Path.Combine(temp.Path, "source");
    var target = Path.Combine(temp.Path, "target");
    var preRestore = Path.Combine(temp.Path, "pre");

    Directory.CreateDirectory(Path.Combine(source, "Characters"));
    await File.WriteAllTextAsync(Path.Combine(source, "Characters", "hero.dat"), "new-save");
    Directory.CreateDirectory(target);
    await File.WriteAllTextAsync(Path.Combine(target, "old.txt"), "old-save");

    var archivePath = Path.Combine(temp.Path, "backup.zip");
    var service = new ArchiveService();
    await service.CreateBackupArchiveAsync(source, archivePath);
    var result = await service.RestoreArchiveAsync(archivePath, target, preRestore);

    AssertEqual("new-save", await File.ReadAllTextAsync(Path.Combine(target, "Characters", "hero.dat")));
    Assert(!File.Exists(Path.Combine(target, "old.txt")), "old target file should be replaced");
    Assert(result.PreRestoreBackupPath is not null, "pre-restore path should be returned");
    Assert(File.Exists(result.PreRestoreBackupPath!), "pre-restore archive should exist");
}

static async Task TestGitHubCreate()
{
    using var temp = new TempDirectory();
    var uploadFile = Path.Combine(temp.Path, "latest.zip");
    await File.WriteAllTextAsync(uploadFile, "zip");

    var handler = new FakeGitHubHandler(HttpStatusCode.NotFound, null);
    using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://api.github.test/") };
    var client = new GitHubContentClient(httpClient, TestSettings(), "token");

    await client.UploadFileAsync(uploadFile, "commit");

    AssertEqual(2, handler.Requests.Count);
    AssertEqual(HttpMethod.Get, handler.Requests[0].Method);
    AssertEqual(HttpMethod.Put, handler.Requests[1].Method);
    Assert(!handler.LastPutBody!.Contains("\"sha\""), "create request should not send sha");
}

static async Task TestCharacterFiles()
{
    using var temp = new TempDirectory();
    var characters = Path.Combine(temp.Path, "Characters");
    Directory.CreateDirectory(characters);
    await File.WriteAllTextAsync(Path.Combine(characters, "Hero One.dat"), "hero");
    await File.WriteAllTextAsync(Path.Combine(characters, "Second.dat"), "hero2");

    var files = CharacterFile.FindInProfile(temp.Path);

    AssertEqual(2, files.Count);
    AssertEqual("character/hero-one", CharacterFile.CreateBranchName("Hero One.dat"));
    AssertEqual("minecraft-dungeons/characters/Hero One.dat", CharacterFile.CreateRemotePath("Hero One.dat"));
}

static async Task TestGitHubUpdate()
{
    using var temp = new TempDirectory();
    var uploadFile = Path.Combine(temp.Path, "latest.zip");
    await File.WriteAllTextAsync(uploadFile, "zip");

    var handler = new FakeGitHubHandler(HttpStatusCode.OK, "{\"sha\":\"abc123\"}");
    using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://api.github.test/") };
    var client = new GitHubContentClient(httpClient, TestSettings(), "token");

    await client.UploadFileAsync(uploadFile, "commit");

    AssertEqual(2, handler.Requests.Count);
    Assert(handler.LastPutBody!.Contains("\"sha\":\"abc123\""), "update request should send current sha");
}

static async Task TestGitHubCharacterBranchCreate()
{
    using var temp = new TempDirectory();
    var uploadFile = Path.Combine(temp.Path, "Hero.dat");
    await File.WriteAllTextAsync(uploadFile, "character-data");

    var handler = new CharacterBranchGitHubHandler();
    using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://api.github.test/") };
    var client = new GitHubContentClient(httpClient, TestSettings(), "token");

    await client.UploadCharacterFileAsync(uploadFile, "character/hero", "minecraft-dungeons/characters/Hero.dat", "commit");

    Assert(handler.Requests.Any(request => request.Method == HttpMethod.Post && request.RequestUri!.AbsolutePath.EndsWith("/git/refs", StringComparison.Ordinal)), "branch create request should be sent");
    Assert(handler.LastReferenceCreateBody!.Contains("\"ref\":\"refs/heads/character/hero\""), "branch create request should target character branch");
    Assert(handler.LastPutBody!.Contains("\"branch\":\"character/hero\""), "character upload should target character branch");
}

static async Task TestGitHubBranchListAndDownload()
{
    using var temp = new TempDirectory();
    var targetFile = Path.Combine(temp.Path, "Hero.dat");

    var handler = new BranchListAndDownloadGitHubHandler();
    using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://api.github.test/") };
    var client = new GitHubContentClient(httpClient, TestSettings(), "token");

    var branches = await client.ListBranchesAsync();
    await client.DownloadFileAsync("minecraft-dungeons/characters/Hero.dat", "character/hero", targetFile);

    AssertEqual(2, branches.Count);
    Assert(branches.Contains("main"), "branches should contain main");
    Assert(branches.Contains("character/hero"), "branches should contain character branch");
    AssertEqual("downloaded-character", await File.ReadAllTextAsync(targetFile));
    Assert(handler.Requests.Any(request => request.RequestUri!.Query.Contains("ref=character%2Fhero", StringComparison.Ordinal)), "download should request selected branch ref");
}

static AppSettings TestSettings()
{
    return new AppSettings
    {
        Owner = "owner",
        Repo = "repo",
        Branch = "main",
        RemotePath = "minecraft-dungeons/default/latest.zip",
        LocalSaveProfilePath = "unused"
    };
}

static void Assert(bool condition, string message)
{
    if (!condition)
    {
        throw new InvalidOperationException(message);
    }
}

static void AssertEqual<T>(T expected, T actual)
{
    if (!EqualityComparer<T>.Default.Equals(expected, actual))
    {
        throw new InvalidOperationException($"Expected '{expected}', got '{actual}'.");
    }
}

sealed class TempDirectory : IDisposable
{
    public TempDirectory()
    {
        Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"mcd-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(Path);
    }

    public string Path { get; }

    public void Dispose()
    {
        if (Directory.Exists(Path))
        {
            Directory.Delete(Path, recursive: true);
        }
    }
}

sealed class FakeGitHubHandler : HttpMessageHandler
{
    private readonly HttpStatusCode getStatusCode;
    private readonly string? getBody;

    public FakeGitHubHandler(HttpStatusCode getStatusCode, string? getBody)
    {
        this.getStatusCode = getStatusCode;
        this.getBody = getBody;
    }

    public List<HttpRequestMessage> Requests { get; } = [];
    public string? LastPutBody { get; private set; }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        Requests.Add(request);

        if (request.Method == HttpMethod.Get)
        {
            return new HttpResponseMessage(getStatusCode)
            {
                Content = new StringContent(getBody ?? "{\"message\":\"not found\"}", Encoding.UTF8, "application/json")
            };
        }

        if (request.Method == HttpMethod.Put)
        {
            LastPutBody = await request.Content!.ReadAsStringAsync(cancellationToken);
            return new HttpResponseMessage(HttpStatusCode.Created)
            {
                Content = new StringContent("{}", Encoding.UTF8, "application/json")
            };
        }

        return new HttpResponseMessage(HttpStatusCode.BadRequest);
    }
}

sealed class CharacterBranchGitHubHandler : HttpMessageHandler
{
    public List<HttpRequestMessage> Requests { get; } = [];
    public string? LastReferenceCreateBody { get; private set; }
    public string? LastPutBody { get; private set; }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        Requests.Add(request);
        var path = request.RequestUri!.AbsolutePath;

        if (request.Method == HttpMethod.Get && path.EndsWith("/git/ref/heads/character/hero", StringComparison.Ordinal))
        {
            return Json(HttpStatusCode.NotFound, "{\"message\":\"not found\"}");
        }

        if (request.Method == HttpMethod.Get && path.EndsWith("/git/ref/heads/main", StringComparison.Ordinal))
        {
            return Json(HttpStatusCode.OK, "{\"object\":{\"sha\":\"base-sha\"}}");
        }

        if (request.Method == HttpMethod.Post && path.EndsWith("/git/refs", StringComparison.Ordinal))
        {
            LastReferenceCreateBody = await request.Content!.ReadAsStringAsync(cancellationToken);
            return Json(HttpStatusCode.Created, "{}");
        }

        if (request.Method == HttpMethod.Get && path.Contains("/contents/", StringComparison.Ordinal))
        {
            return Json(HttpStatusCode.NotFound, "{\"message\":\"not found\"}");
        }

        if (request.Method == HttpMethod.Put && path.Contains("/contents/", StringComparison.Ordinal))
        {
            LastPutBody = await request.Content!.ReadAsStringAsync(cancellationToken);
            return Json(HttpStatusCode.Created, "{}");
        }

        return Json(HttpStatusCode.BadRequest, "{}");
    }

    private static HttpResponseMessage Json(HttpStatusCode statusCode, string body)
    {
        return new HttpResponseMessage(statusCode)
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json")
        };
    }
}

sealed class BranchListAndDownloadGitHubHandler : HttpMessageHandler
{
    public List<HttpRequestMessage> Requests { get; } = [];

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        Requests.Add(request);
        var path = request.RequestUri!.AbsolutePath;

        if (request.Method == HttpMethod.Get && path.EndsWith("/branches", StringComparison.Ordinal))
        {
            return Task.FromResult(Json(HttpStatusCode.OK, "[{\"name\":\"main\"},{\"name\":\"character/hero\"}]"));
        }

        if (request.Method == HttpMethod.Get && path.Contains("/contents/", StringComparison.Ordinal))
        {
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("downloaded-character", Encoding.UTF8, "application/octet-stream")
            });
        }

        return Task.FromResult(Json(HttpStatusCode.BadRequest, "{}"));
    }

    private static HttpResponseMessage Json(HttpStatusCode statusCode, string body)
    {
        return new HttpResponseMessage(statusCode)
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json")
        };
    }
}
