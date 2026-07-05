namespace McDungeonsGitBackup.Core;

public sealed class AppSettings
{
    public string Owner { get; set; } = "";
    public string Repo { get; set; } = "";
    public string Branch { get; set; } = "main";
    public string RemotePath { get; set; } = "minecraft-dungeons/default/latest.zip";
    public string LocalSaveProfilePath { get; set; } = "";

    public IReadOnlyList<string> ValidateForGitHub()
    {
        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(Owner))
        {
            errors.Add("GitHub owner is required.");
        }

        if (string.IsNullOrWhiteSpace(Repo))
        {
            errors.Add("GitHub repo is required.");
        }

        if (string.IsNullOrWhiteSpace(Branch))
        {
            errors.Add("GitHub branch is required.");
        }

        if (string.IsNullOrWhiteSpace(RemotePath))
        {
            errors.Add("Remote path is required.");
        }

        return errors;
    }

    public IReadOnlyList<string> ValidateForSaveOperations()
    {
        var errors = ValidateForGitHub().ToList();

        if (string.IsNullOrWhiteSpace(LocalSaveProfilePath))
        {
            errors.Add("Local save profile path is required.");
        }
        else if (!Directory.Exists(LocalSaveProfilePath))
        {
            errors.Add("Local save profile path does not exist.");
        }

        return errors;
    }
}
