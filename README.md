# Minecraft Dungeons Character Saver

Open-source Windows app for saving and restoring Minecraft Dungeons characters through GitHub.

The app is built around a simple branch model:

- `main` stores the full save profile as one validated zip archive.
- Any other branch stores the selected character `.dat` file.
- If `main` is selected and you click `Save character`, the app automatically creates or updates the character branch for the selected `.dat` file.
- Restoring from `main` restores all saves.
- Restoring from any other branch restores only the selected character.

## Features

- Dark, large, minimal WinForms interface.
- Auto-detects Minecraft Dungeons profiles that contain `Characters/*.dat`.
- Shows local character `.dat` files in a dropdown.
- Lets you select or type the current GitHub branch.
- Loads branch names from GitHub.
- Saves the whole profile to `main`.
- Saves the selected character to the current branch, or to the auto-generated character branch when `main` is selected.
- Creates a new character branch from `main` if it does not exist yet.
- Suggests the character branch for the currently selected `.dat` file.
- Restores all saves from `main`.
- Restores one selected character from the currently selected non-main branch.
- Creates local pre-restore backups before replacing files.
- Stores the GitHub token in Windows Credential Manager, not in config files.

## GitHub Token

Create a fine-grained personal access token:

1. Open <https://github.com/settings/tokens?type=beta>.
2. Generate a new fine-grained token.
3. Repository access: select only your backup repository.
4. Repository permissions: `Contents` = `Read and write`.
5. Copy the token and paste it into the app settings.

## App Settings Example

```text
GitHub owner:
offlayt

Repository:
MDCS-archive

Branch:
main

Remote path:
minecraft-dungeons/default/latest.zip

Local save path:
C:\Users\YOUR_USER\Saved Games\Mojang Studios\Dungeons\<profile-id>
```

The local save path should be the folder that contains the `Characters` directory.

## Daily Use

### Save All

1. Select `main` as the current branch.
2. Click `Save all`.

The app zips the whole selected save profile, adds `backup-manifest.json`, and uploads the archive to:

```text
main / minecraft-dungeons/default/latest.zip
```

### Save One Character

1. Select or type a non-main branch, for example:

```text
character/hero-one
```

2. Select the local `.dat` character.
3. Click `Save character`.

You can also leave `main` selected. In that case `Save character` automatically uses or creates:

```text
character/<selected-dat-filename>
```

The app uploads the selected file to:

```text
<current-branch> / minecraft-dungeons/characters/<selected-file>.dat
```

### Restore

Click `Restore current`.

If the current branch is `main`, the app downloads and restores the full zip archive.

If the current branch is not `main`, the app downloads only:

```text
minecraft-dungeons/characters/<selected-file>.dat
```

and replaces only the selected local character file.

Close Minecraft Dungeons before saving or restoring.

## Build

Requirements:

- Windows
- .NET SDK 8

Build:

```powershell
dotnet build McDungeonsGitBackup.sln
```

Run tests:

```powershell
dotnet run --project tests\McDungeonsGitBackup.Tests\McDungeonsGitBackup.Tests.csproj
```

Publish normal portable exe:

```powershell
dotnet publish src\McDungeonsGitBackup.App\McDungeonsGitBackup.App.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:EnableCompressionInSingleFile=false -o outputs\McDungeonsGitBackup-normal
```

This is the classic self-contained build. It runs without installing .NET, but the file is larger.

Publish smaller portable exe:

```powershell
dotnet publish src\McDungeonsGitBackup.App\McDungeonsGitBackup.App.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o outputs\McDungeonsGitBackup-portable
```

This is the recommended release build. It is self-contained, compressed, and runs on another Windows PC without installing the .NET SDK or runtime.

Publish lite exe:

```powershell
dotnet publish src\McDungeonsGitBackup.App\McDungeonsGitBackup.App.csproj -c Release -r win-x64 --self-contained false -p:PublishSingleFile=true -o outputs\McDungeonsGitBackup-lite
```

The small build is much lighter, but the target PC must have the [.NET 8 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet/8.0) installed.

Size notes from the current project:

- old uncompressed portable exe: about 154 MB
- compressed portable exe: about 68 MB
- framework-dependent lite exe: about 0.3 MB

Trimming is not enabled because Windows Forms is not supported with `PublishTrimmed` and the SDK blocks that publish mode.

Release assets use these names:

- `McDungeonsGitBackup-normal.exe` - normal self-contained build, largest file.
- `McDungeonsGitBackup-portable-small.exe` - recommended self-contained compressed build.
- `McDungeonsGitBackup-lite.exe` - smallest build, requires .NET 8 Desktop Runtime.

## Project Layout

- `src/McDungeonsGitBackup.App` - WinForms interface.
- `src/McDungeonsGitBackup.Core` - save discovery, archive, GitHub API, settings and credentials.
- `tests/McDungeonsGitBackup.Tests` - no-NuGet test runner.
- Release builds are published as GitHub Release assets.

## License

MIT License.
