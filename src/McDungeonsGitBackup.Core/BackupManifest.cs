namespace McDungeonsGitBackup.Core;

public sealed class BackupManifest
{
    public int Version { get; set; } = 1;
    public DateTimeOffset CreatedUtc { get; set; }
    public string MachineName { get; set; } = "";
    public string SourcePath { get; set; } = "";
    public List<BackupFileEntry> Files { get; set; } = [];
}

public sealed class BackupFileEntry
{
    public string RelativePath { get; set; } = "";
    public long Length { get; set; }
    public string Sha256 { get; set; } = "";
}
