namespace McDungeonsGitBackup.Core;

public sealed record SaveProfile(string Path, int CharacterCount)
{
    public override string ToString()
    {
        return $"{Path} ({CharacterCount} character .dat file{(CharacterCount == 1 ? "" : "s")})";
    }
}
