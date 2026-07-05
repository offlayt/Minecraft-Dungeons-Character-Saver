using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace McDungeonsGitBackup.Core;

public sealed class GitHubContentClient
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private readonly HttpClient httpClient;
    private readonly AppSettings settings;
    private readonly string token;

    public GitHubContentClient(HttpClient httpClient, AppSettings settings, string token)
    {
        this.httpClient = httpClient;
        this.settings = settings;
        this.token = token;
    }

    public async Task TestConnectionAsync(CancellationToken cancellationToken = default)
    {
        using var request = CreateRequest(HttpMethod.Get, $"repos/{settings.Owner}/{settings.Repo}");
        using var response = await httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
        {
            throw await CreateGitHubExceptionAsync(response, "GitHub connection test failed.", cancellationToken).ConfigureAwait(false);
        }
    }

    public async Task<IReadOnlyList<string>> ListBranchesAsync(CancellationToken cancellationToken = default)
    {
        using var request = CreateRequest(HttpMethod.Get, $"repos/{settings.Owner}/{settings.Repo}/branches?per_page=100");
        using var response = await httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
        {
            throw await CreateGitHubExceptionAsync(response, "GitHub branch list failed.", cancellationToken).ConfigureAwait(false);
        }

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        var branches = await JsonSerializer.DeserializeAsync<List<BranchResponse>>(stream, JsonOptions, cancellationToken).ConfigureAwait(false);
        return branches?
            .Select(branch => branch.Name)
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .OrderBy(name => name.Equals(settings.Branch, StringComparison.OrdinalIgnoreCase) ? 0 : 1)
            .ThenBy(name => name, StringComparer.OrdinalIgnoreCase)
            .ToList() ?? [];
    }

    public async Task UploadFileAsync(string localFilePath, string commitMessage, CancellationToken cancellationToken = default)
    {
        await UploadFileAsync(localFilePath, settings.RemotePath, settings.Branch, commitMessage, cancellationToken).ConfigureAwait(false);
    }

    public async Task UploadCharacterFileAsync(
        string localFilePath,
        string branch,
        string remotePath,
        string commitMessage,
        CancellationToken cancellationToken = default)
    {
        await EnsureBranchExistsAsync(branch, settings.Branch, cancellationToken).ConfigureAwait(false);
        await UploadFileAsync(localFilePath, remotePath, branch, commitMessage, cancellationToken).ConfigureAwait(false);
    }

    public async Task UploadFileAsync(
        string localFilePath,
        string remotePath,
        string branch,
        string commitMessage,
        CancellationToken cancellationToken = default)
    {
        if (!File.Exists(localFilePath))
        {
            throw new FileNotFoundException("File to upload does not exist.", localFilePath);
        }

        var currentSha = await GetCurrentShaAsync(remotePath, branch, cancellationToken).ConfigureAwait(false);
        var content = Convert.ToBase64String(await File.ReadAllBytesAsync(localFilePath, cancellationToken).ConfigureAwait(false));
        var payload = new PutContentRequest(commitMessage, content, branch, currentSha);
        var json = JsonSerializer.Serialize(payload, JsonOptions);

        using var request = CreateRequest(HttpMethod.Put, $"repos/{settings.Owner}/{settings.Repo}/contents/{EscapePath(remotePath)}");
        request.Content = new StringContent(json, Encoding.UTF8, "application/json");

        using var response = await httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
        {
            throw await CreateGitHubExceptionAsync(response, "GitHub upload failed.", cancellationToken).ConfigureAwait(false);
        }
    }

    public async Task EnsureBranchExistsAsync(string branch, string baseBranch, CancellationToken cancellationToken = default)
    {
        if (await TryGetReferenceShaAsync(branch, cancellationToken).ConfigureAwait(false) is not null)
        {
            return;
        }

        var baseSha = await TryGetReferenceShaAsync(baseBranch, cancellationToken).ConfigureAwait(false)
            ?? throw new GitHubContentException($"Base branch '{baseBranch}' was not found.");
        var payload = new CreateReferenceRequest($"refs/heads/{branch}", baseSha);
        var json = JsonSerializer.Serialize(payload, JsonOptions);

        using var request = CreateRequest(HttpMethod.Post, $"repos/{settings.Owner}/{settings.Repo}/git/refs");
        request.Content = new StringContent(json, Encoding.UTF8, "application/json");

        using var response = await httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
        if (response.StatusCode == HttpStatusCode.UnprocessableEntity)
        {
            return;
        }

        if (!response.IsSuccessStatusCode)
        {
            throw await CreateGitHubExceptionAsync(response, "GitHub branch create failed.", cancellationToken).ConfigureAwait(false);
        }
    }

    public async Task DownloadFileAsync(string localFilePath, CancellationToken cancellationToken = default)
    {
        await DownloadFileAsync(settings.RemotePath, settings.Branch, localFilePath, cancellationToken).ConfigureAwait(false);
    }

    public async Task DownloadFileAsync(string remotePath, string branch, string localFilePath, CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(localFilePath))!);

        var uri = $"repos/{settings.Owner}/{settings.Repo}/contents/{EscapePath(remotePath)}?ref={Uri.EscapeDataString(branch)}";
        using var request = CreateRequest(HttpMethod.Get, uri);
        request.Headers.Accept.Clear();
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github.raw+json"));

        using var response = await httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
        {
            throw await CreateGitHubExceptionAsync(response, "GitHub download failed.", cancellationToken).ConfigureAwait(false);
        }

        await using var source = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        await using var destination = File.Create(localFilePath);
        await source.CopyToAsync(destination, cancellationToken).ConfigureAwait(false);
    }

    private async Task<string?> GetCurrentShaAsync(string remotePath, string branch, CancellationToken cancellationToken)
    {
        var uri = $"repos/{settings.Owner}/{settings.Repo}/contents/{EscapePath(remotePath)}?ref={Uri.EscapeDataString(branch)}";
        using var request = CreateRequest(HttpMethod.Get, uri);

        using var response = await httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        if (!response.IsSuccessStatusCode)
        {
            throw await CreateGitHubExceptionAsync(response, "GitHub metadata read failed.", cancellationToken).ConfigureAwait(false);
        }

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        var content = await JsonSerializer.DeserializeAsync<GetContentResponse>(stream, JsonOptions, cancellationToken).ConfigureAwait(false);
        return content?.Sha;
    }

    private async Task<string?> TryGetReferenceShaAsync(string branch, CancellationToken cancellationToken)
    {
        using var request = CreateRequest(HttpMethod.Get, $"repos/{settings.Owner}/{settings.Repo}/git/ref/heads/{EscapePath(branch)}");

        using var response = await httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        if (!response.IsSuccessStatusCode)
        {
            throw await CreateGitHubExceptionAsync(response, "GitHub branch read failed.", cancellationToken).ConfigureAwait(false);
        }

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        var content = await JsonSerializer.DeserializeAsync<GetReferenceResponse>(stream, JsonOptions, cancellationToken).ConfigureAwait(false);
        return content?.Object?.Sha;
    }

    private HttpRequestMessage CreateRequest(HttpMethod method, string relativeUri)
    {
        var request = new HttpRequestMessage(method, new Uri(GetBaseAddress(), relativeUri));
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        request.Headers.UserAgent.ParseAdd("McDungeonsGitBackup/1.0");
        request.Headers.Add("X-GitHub-Api-Version", "2022-11-28");
        return request;
    }

    private Uri GetBaseAddress()
    {
        return httpClient.BaseAddress ?? new Uri("https://api.github.com/");
    }

    private static string EscapePath(string path)
    {
        return string.Join("/", path.Split(['/', '\\'], StringSplitOptions.RemoveEmptyEntries).Select(Uri.EscapeDataString));
    }

    private static async Task<GitHubContentException> CreateGitHubExceptionAsync(HttpResponseMessage response, string prefix, CancellationToken cancellationToken)
    {
        var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        string? message = null;

        try
        {
            message = JsonSerializer.Deserialize<GitHubErrorResponse>(body, JsonOptions)?.Message;
        }
        catch
        {
            // Keep the raw body fallback below.
        }

        message ??= string.IsNullOrWhiteSpace(body) ? response.ReasonPhrase : body;
        return new GitHubContentException($"{prefix} HTTP {(int)response.StatusCode}: {message}");
    }

    private sealed record GetContentResponse([property: JsonPropertyName("sha")] string? Sha);

    private sealed record BranchResponse([property: JsonPropertyName("name")] string Name);

    private sealed record GetReferenceResponse([property: JsonPropertyName("object")] ReferenceObject? Object);

    private sealed record ReferenceObject([property: JsonPropertyName("sha")] string? Sha);

    private sealed record PutContentRequest(
        [property: JsonPropertyName("message")] string Message,
        [property: JsonPropertyName("content")] string Content,
        [property: JsonPropertyName("branch")] string Branch,
        [property: JsonPropertyName("sha")] string? Sha);

    private sealed record CreateReferenceRequest(
        [property: JsonPropertyName("ref")] string Ref,
        [property: JsonPropertyName("sha")] string Sha);

    private sealed record GitHubErrorResponse([property: JsonPropertyName("message")] string? Message);
}

public sealed class GitHubContentException : Exception
{
    public GitHubContentException(string message)
        : base(message)
    {
    }
}
