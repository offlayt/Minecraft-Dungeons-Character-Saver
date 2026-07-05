namespace McDungeonsGitBackup.Core;

public sealed record CharacterFile(string FullPath, string FileName, long Length, DateTime LastWriteTime)
{
    public string DisplayName => $"{FileName}  |  {FormatSize(Length)}  |  {LastWriteTime:g}";

    public static IReadOnlyList<CharacterFile> FindInProfile(string profilePath)
    {
        var charactersDirectory = Path.Combine(profilePath, "Characters");
        if (!Directory.Exists(charactersDirectory))
        {
            return [];
        }

        return Directory.EnumerateFiles(charactersDirectory, "*.dat", SearchOption.TopDirectoryOnly)
            .Select(path =>
            {
                var info = new FileInfo(path);
                return new CharacterFile(path, info.Name, info.Length, info.LastWriteTime);
            })
            .OrderByDescending(file => file.LastWriteTime)
            .ThenBy(file => file.FileName, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public static string CreateBranchName(string fileName)
    {
        var nameWithoutExtension = Path.GetFileNameWithoutExtension(fileName);
        var chars = nameWithoutExtension
            .Select(character => char.IsLetterOrDigit(character) ? char.ToLowerInvariant(character) : '-')
            .ToArray();
        var safe = new string(chars).Trim('-');

        while (safe.Contains("--", StringComparison.Ordinal))
        {
            safe = safe.Replace("--", "-", StringComparison.Ordinal);
        }

        if (string.IsNullOrWhiteSpace(safe))
        {
            safe = "character";
        }

        return $"character/{safe}";
    }

    public static string CreateRemotePath(string fileName)
    {
        return $"minecraft-dungeons/characters/{Path.GetFileName(fileName)}";
    }

    private static string FormatSize(long bytes)
    {
        if (bytes < 1024)
        {
            return $"{bytes} B";
        }

        if (bytes < 1024 * 1024)
        {
            return $"{bytes / 1024d:0.0} KB";
        }

        return $"{bytes / 1024d / 1024d:0.0} MB";
    }
}
