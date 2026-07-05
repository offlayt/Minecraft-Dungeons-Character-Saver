using System.Diagnostics;

namespace McDungeonsGitBackup.Core;

public sealed class GameProcessGuard
{
    private static readonly HashSet<string> GameProcessNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "Dungeons",
        "Dungeons-Win64-Shipping",
        "MinecraftDungeons",
        "Minecraft Dungeons"
    };

    public bool IsGameRunning(out string processNames)
    {
        var currentProcessId = Environment.ProcessId;
        var matches = Process.GetProcesses()
            .Where(process =>
            {
                try
                {
                    return process.Id != currentProcessId && GameProcessNames.Contains(process.ProcessName);
                }
                catch
                {
                    return false;
                }
            })
            .Select(process => process.ProcessName)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
            .ToList();

        processNames = string.Join(", ", matches);
        return matches.Count > 0;
    }
}
