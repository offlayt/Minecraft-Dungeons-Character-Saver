using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;

namespace McDungeonsGitBackup.Core;

public sealed class ArchiveService
{
    public const string ManifestEntryName = "backup-manifest.json";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    public async Task<BackupManifest> CreateBackupArchiveAsync(string sourceDirectory, string archivePath, CancellationToken cancellationToken = default)
    {
        if (!Directory.Exists(sourceDirectory))
        {
            throw new DirectoryNotFoundException($"Source directory does not exist: {sourceDirectory}");
        }

        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(archivePath))!);
        if (File.Exists(archivePath))
        {
            File.Delete(archivePath);
        }

        var manifest = await BuildManifestAsync(sourceDirectory, cancellationToken).ConfigureAwait(false);

        using var archiveStream = File.Create(archivePath);
        using var archive = new ZipArchive(archiveStream, ZipArchiveMode.Create);

        foreach (var file in manifest.Files)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var fullPath = Path.Combine(sourceDirectory, NormalizeForCurrentPlatform(file.RelativePath));
            var entry = archive.CreateEntry(file.RelativePath, CompressionLevel.Optimal);
            await using var entryStream = entry.Open();
            await using var fileStream = File.OpenRead(fullPath);
            await fileStream.CopyToAsync(entryStream, cancellationToken).ConfigureAwait(false);
        }

        var manifestEntry = archive.CreateEntry(ManifestEntryName, CompressionLevel.Optimal);
        await using (var manifestStream = manifestEntry.Open())
        {
            await JsonSerializer.SerializeAsync(manifestStream, manifest, JsonOptions, cancellationToken).ConfigureAwait(false);
        }

        return manifest;
    }

    public async Task<RestoreResult> RestoreArchiveAsync(
        string archivePath,
        string targetDirectory,
        string preRestoreBackupDirectory,
        CancellationToken cancellationToken = default)
    {
        if (!File.Exists(archivePath))
        {
            throw new FileNotFoundException("Backup archive does not exist.", archivePath);
        }

        EnsureSafeRestoreTarget(targetDirectory);
        var manifest = await ValidateArchiveAsync(archivePath, cancellationToken).ConfigureAwait(false);
        string? preRestoreBackupPath = null;

        if (Directory.Exists(targetDirectory) && Directory.EnumerateFileSystemEntries(targetDirectory).Any())
        {
            Directory.CreateDirectory(preRestoreBackupDirectory);
            preRestoreBackupPath = Path.Combine(
                preRestoreBackupDirectory,
                $"pre-restore-{DateTimeOffset.Now:yyyyMMdd-HHmmss}.zip");
            await CreateBackupArchiveAsync(targetDirectory, preRestoreBackupPath, cancellationToken).ConfigureAwait(false);
        }

        Directory.CreateDirectory(targetDirectory);
        ClearDirectory(targetDirectory);

        using var archive = ZipFile.OpenRead(archivePath);
        foreach (var file in manifest.Files)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var entry = archive.GetEntry(file.RelativePath)
                ?? throw new InvalidDataException($"Archive entry is missing: {file.RelativePath}");
            var destinationPath = GetSafeDestinationPath(targetDirectory, file.RelativePath);
            Directory.CreateDirectory(Path.GetDirectoryName(destinationPath)!);
            entry.ExtractToFile(destinationPath, overwrite: true);
        }

        return new RestoreResult(manifest, preRestoreBackupPath);
    }

    public async Task<BackupManifest> ValidateArchiveAsync(string archivePath, CancellationToken cancellationToken = default)
    {
        using var archive = ZipFile.OpenRead(archivePath);
        var manifestEntry = archive.GetEntry(ManifestEntryName)
            ?? throw new InvalidDataException("Backup archive does not contain backup-manifest.json.");

        BackupManifest? manifest;
        await using (var stream = manifestEntry.Open())
        {
            manifest = await JsonSerializer.DeserializeAsync<BackupManifest>(stream, JsonOptions, cancellationToken).ConfigureAwait(false);
        }

        if (manifest is null || manifest.Version != 1)
        {
            throw new InvalidDataException("Backup manifest is missing or unsupported.");
        }

        foreach (var file in manifest.Files)
        {
            cancellationToken.ThrowIfCancellationRequested();

            ValidateRelativePath(file.RelativePath);
            var entry = archive.GetEntry(file.RelativePath)
                ?? throw new InvalidDataException($"Archive entry is missing: {file.RelativePath}");
            if (entry.Length != file.Length)
            {
                throw new InvalidDataException($"Archive entry size mismatch: {file.RelativePath}");
            }

            await using var stream = entry.Open();
            var hash = await ComputeSha256Async(stream, cancellationToken).ConfigureAwait(false);
            if (!string.Equals(hash, file.Sha256, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidDataException($"Archive entry hash mismatch: {file.RelativePath}");
            }
        }

        return manifest;
    }

    private static async Task<BackupManifest> BuildManifestAsync(string sourceDirectory, CancellationToken cancellationToken)
    {
        var fullSourceDirectory = Path.GetFullPath(sourceDirectory);
        var manifest = new BackupManifest
        {
            CreatedUtc = DateTimeOffset.UtcNow,
            MachineName = Environment.MachineName,
            SourcePath = fullSourceDirectory
        };

        var files = Directory.EnumerateFiles(fullSourceDirectory, "*", SearchOption.AllDirectories)
            .Where(file => !string.Equals(
                ToArchivePath(Path.GetRelativePath(fullSourceDirectory, file)),
                ManifestEntryName,
                StringComparison.OrdinalIgnoreCase))
            .OrderBy(file => file, StringComparer.OrdinalIgnoreCase);

        foreach (var file in files)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var info = new FileInfo(file);
            await using var stream = File.OpenRead(file);
            manifest.Files.Add(new BackupFileEntry
            {
                RelativePath = ToArchivePath(Path.GetRelativePath(fullSourceDirectory, file)),
                Length = info.Length,
                Sha256 = await ComputeSha256Async(stream, cancellationToken).ConfigureAwait(false)
            });
        }

        return manifest;
    }

    private static async Task<string> ComputeSha256Async(Stream stream, CancellationToken cancellationToken)
    {
        var hash = await SHA256.HashDataAsync(stream, cancellationToken).ConfigureAwait(false);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    private static string ToArchivePath(string relativePath)
    {
        return relativePath.Replace('\\', '/');
    }

    private static string NormalizeForCurrentPlatform(string relativePath)
    {
        return relativePath.Replace('/', Path.DirectorySeparatorChar);
    }

    private static void ClearDirectory(string directory)
    {
        foreach (var file in Directory.EnumerateFiles(directory))
        {
            File.SetAttributes(file, FileAttributes.Normal);
            File.Delete(file);
        }

        foreach (var childDirectory in Directory.EnumerateDirectories(directory))
        {
            SetNormalAttributes(childDirectory);
            Directory.Delete(childDirectory, recursive: true);
        }
    }

    private static void SetNormalAttributes(string directory)
    {
        foreach (var file in Directory.EnumerateFiles(directory, "*", SearchOption.AllDirectories))
        {
            File.SetAttributes(file, FileAttributes.Normal);
        }
    }

    private static string GetSafeDestinationPath(string targetDirectory, string relativePath)
    {
        ValidateRelativePath(relativePath);

        var fullTarget = Path.GetFullPath(targetDirectory);
        var destination = Path.GetFullPath(Path.Combine(fullTarget, NormalizeForCurrentPlatform(relativePath)));
        if (!destination.StartsWith(fullTarget.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException($"Archive entry escapes target directory: {relativePath}");
        }

        return destination;
    }

    private static void ValidateRelativePath(string relativePath)
    {
        var segments = relativePath.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (string.IsNullOrWhiteSpace(relativePath)
            || Path.IsPathFullyQualified(relativePath)
            || relativePath.Contains('\\')
            || segments.Length == 0
            || segments.Any(segment => segment is "." or ".."))
        {
            throw new InvalidDataException($"Unsafe archive path: {relativePath}");
        }
    }

    private static void EnsureSafeRestoreTarget(string targetDirectory)
    {
        var fullPath = Path.GetFullPath(targetDirectory);
        var root = Path.GetPathRoot(fullPath);

        if (string.IsNullOrWhiteSpace(targetDirectory)
            || string.Equals(fullPath.TrimEnd(Path.DirectorySeparatorChar), root?.TrimEnd(Path.DirectorySeparatorChar), StringComparison.OrdinalIgnoreCase)
            || fullPath.Length < 10)
        {
            throw new InvalidOperationException("Refusing to restore into an unsafe target directory.");
        }
    }
}

public sealed record RestoreResult(BackupManifest Manifest, string? PreRestoreBackupPath);
