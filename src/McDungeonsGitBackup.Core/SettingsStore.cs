using System.Text.Json;

namespace McDungeonsGitBackup.Core;

public sealed class SettingsStore
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    public SettingsStore(string? appDataDirectory = null)
    {
        AppDataDirectory = appDataDirectory ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "McDungeonsGitBackup");
        SettingsPath = Path.Combine(AppDataDirectory, "settings.json");
    }

    public string AppDataDirectory { get; }

    public string SettingsPath { get; }

    public AppSettings Load()
    {
        if (!File.Exists(SettingsPath))
        {
            return new AppSettings();
        }

        try
        {
            var json = File.ReadAllText(SettingsPath);
            return JsonSerializer.Deserialize<AppSettings>(json, JsonOptions) ?? new AppSettings();
        }
        catch
        {
            return new AppSettings();
        }
    }

    public void Save(AppSettings settings)
    {
        Directory.CreateDirectory(AppDataDirectory);
        var json = JsonSerializer.Serialize(settings, JsonOptions);
        File.WriteAllText(SettingsPath, json);
    }

    public string GetPreRestoreBackupDirectory()
    {
        return Path.Combine(AppDataDirectory, "pre-restore-backups");
    }
}
