using McDungeonsGitBackup.Core;

namespace McDungeonsGitBackup.App;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        ApplicationConfiguration.Initialize();
        Application.Run(new MainForm(new SettingsStore(), new WindowsCredentialStore()));
    }
}
