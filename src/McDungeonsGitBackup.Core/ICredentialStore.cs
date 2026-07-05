namespace McDungeonsGitBackup.Core;

public interface ICredentialStore
{
    string? ReadToken();
    void SaveToken(string token);
    void DeleteToken();
}
