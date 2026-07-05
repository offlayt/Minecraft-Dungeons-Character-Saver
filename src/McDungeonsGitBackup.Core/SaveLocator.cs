namespace McDungeonsGitBackup.Core;

public sealed class SaveLocator
{
    public IReadOnlyList<SaveProfile> FindProfiles()
    {
        return FindProfiles(GetDefaultSearchRoots());
    }

    public IReadOnlyList<SaveProfile> FindProfiles(IEnumerable<string> searchRoots)
    {
        var profiles = new Dictionary<string, SaveProfile>(StringComparer.OrdinalIgnoreCase);

        foreach (var root in searchRoots.Where(Directory.Exists).Distinct(StringComparer.OrdinalIgnoreCase))
        {
            foreach (var charactersDirectory in EnumerateCharacterDirectories(root))
            {
                var profileDirectory = Directory.GetParent(charactersDirectory)?.FullName;
                if (profileDirectory is null || profiles.ContainsKey(profileDirectory))
                {
                    continue;
                }

                var datCount = CountDatFiles(charactersDirectory);
                if (datCount > 0)
                {
                    profiles[profileDirectory] = new SaveProfile(profileDirectory, datCount);
                }
            }
        }

        return profiles.Values
            .OrderBy(profile => profile.Path, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public IReadOnlyList<string> GetDefaultSearchRoots()
    {
        var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);

        var roots = new List<string>();

        if (!string.IsNullOrWhiteSpace(userProfile))
        {
            roots.Add(Path.Combine(userProfile, "Saved Games", "Mojang Studios", "Dungeons"));
            roots.Add(Path.Combine(userProfile, "Saved Games"));
        }

        if (!string.IsNullOrWhiteSpace(localAppData))
        {
            roots.Add(Path.Combine(localAppData, "Dungeons"));
            roots.Add(Path.Combine(localAppData, "Mojang Studios", "Dungeons"));
            roots.Add(Path.Combine(localAppData, "Packages", "Microsoft.Lovika_8wekyb3d8bbwe"));
            roots.Add(Path.Combine(localAppData, "Packages", "Microsoft.MinecraftDungeons_8wekyb3d8bbwe"));
        }

        if (!string.IsNullOrWhiteSpace(appData))
        {
            roots.Add(Path.Combine(appData, "Mojang Studios", "Dungeons"));
        }

        return roots;
    }

    private static IEnumerable<string> EnumerateCharacterDirectories(string root)
    {
        var direct = Path.Combine(root, "Characters");
        if (Directory.Exists(direct))
        {
            yield return direct;
        }

        IEnumerable<string> matches;
        try
        {
            matches = Directory.EnumerateDirectories(root, "Characters", SearchOption.AllDirectories);
        }
        catch
        {
            yield break;
        }

        foreach (var match in matches)
        {
            yield return match;
        }
    }

    private static int CountDatFiles(string charactersDirectory)
    {
        try
        {
            return Directory.EnumerateFiles(charactersDirectory, "*.dat", SearchOption.TopDirectoryOnly).Count();
        }
        catch
        {
            return 0;
        }
    }
}
