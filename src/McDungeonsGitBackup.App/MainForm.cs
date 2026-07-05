using McDungeonsGitBackup.Core;

namespace McDungeonsGitBackup.App;

public sealed class MainForm : Form
{
    private readonly SettingsStore settingsStore;
    private readonly ICredentialStore credentialStore;
    private readonly ArchiveService archiveService = new();
    private readonly GameProcessGuard processGuard = new();

    private readonly Button saveAllButton;
    private readonly Button settingsButton;
    private readonly Button pushCharacterButton;
    private readonly Button restoreButton;
    private readonly Button refreshCharactersButton;
    private readonly Button refreshBranchesButton;
    private readonly Label profileLabel = CreateInfoLabel();
    private readonly Label remoteLabel = CreateInfoLabel();
    private readonly Label modeLabel = CreateInfoLabel(AppTheme.Gold, FontStyle.Bold);
    private readonly Label statusLabel = CreateInfoLabel(AppTheme.MutedText);
    private readonly ComboBox branchBox = new();
    private readonly ComboBox characterBox = new();

    private AppSettings settings;
    private IReadOnlyList<CharacterFile> characters = [];

    public MainForm(SettingsStore settingsStore, ICredentialStore credentialStore)
    {
        this.settingsStore = settingsStore;
        this.credentialStore = credentialStore;
        settings = settingsStore.Load();

        saveAllButton = AppTheme.Button("Save all", AppTheme.Emerald);
        settingsButton = AppTheme.Button("Settings", AppTheme.Border);
        pushCharacterButton = AppTheme.Button("Save character", AppTheme.Gold);
        restoreButton = AppTheme.Button("Restore current", AppTheme.Redstone);
        refreshCharactersButton = AppTheme.Button("Characters", AppTheme.Border);
        refreshBranchesButton = AppTheme.Button("Branches", AppTheme.Border);

        Text = "Minecraft Dungeons Character Saver";
        Width = 1040;
        Height = 620;
        MinimumSize = new Size(900, 560);
        StartPosition = FormStartPosition.CenterScreen;
        AppTheme.StyleForm(this);

        BuildUi();
        RefreshState();
    }

    private void BuildUi()
    {
        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 5,
            Padding = new Padding(22),
            BackColor = AppTheme.Background
        };
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        var title = AppTheme.Label("Minecraft Dungeons Character Saver", 23F, AppTheme.Text, FontStyle.Bold);
        var subtitle = AppTheme.Label("Main = full backup. Character branches are created automatically from the selected .dat file.", 10F, AppTheme.MutedText);

        var header = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            AutoSize = true,
            Margin = new Padding(0, 0, 0, 14),
            BackColor = AppTheme.Background
        };
        header.Controls.Add(title);
        header.Controls.Add(subtitle);

        var infoPanel = CreatePanel(84);
        infoPanel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        infoPanel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        infoPanel.Controls.Add(profileLabel, 0, 0);
        infoPanel.Controls.Add(remoteLabel, 0, 1);

        var pickerPanel = CreatePanel(196);
        pickerPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 44));
        pickerPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 48));
        pickerPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 48));
        pickerPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));

        pickerPanel.Controls.Add(AppTheme.Label("Branch and character", 14F, AppTheme.Text, FontStyle.Bold), 0, 0);
        pickerPanel.Controls.Add(CreateBranchRow(), 0, 1);
        pickerPanel.Controls.Add(CreateCharacterRow(), 0, 2);

        modeLabel.Height = 30;
        modeLabel.Margin = new Padding(0, 6, 0, 0);
        pickerPanel.Controls.Add(modeLabel, 0, 3);

        var buttons = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 2,
            Height = 116,
            BackColor = AppTheme.Background,
            Margin = new Padding(0, 12, 0, 12)
        };
        buttons.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        buttons.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        buttons.RowStyles.Add(new RowStyle(SizeType.Percent, 50));
        buttons.RowStyles.Add(new RowStyle(SizeType.Percent, 50));

        saveAllButton.Dock = DockStyle.Fill;
        settingsButton.Dock = DockStyle.Fill;
        pushCharacterButton.Dock = DockStyle.Fill;
        restoreButton.Dock = DockStyle.Fill;
        saveAllButton.Margin = new Padding(0, 0, 8, 8);
        settingsButton.Margin = new Padding(0, 8, 8, 0);
        pushCharacterButton.Margin = new Padding(8, 0, 0, 8);
        restoreButton.Margin = new Padding(8, 8, 0, 0);

        saveAllButton.Click += async (_, _) => await RunSaveAllAsync();
        settingsButton.Click += (_, _) => OpenSettings();
        pushCharacterButton.Click += async (_, _) => await RunPushCharacterAsync();
        restoreButton.Click += async (_, _) => await RunRestoreCurrentBranchAsync();

        buttons.Controls.Add(saveAllButton, 0, 0);
        buttons.Controls.Add(settingsButton, 0, 1);
        buttons.Controls.Add(pushCharacterButton, 1, 0);
        buttons.Controls.Add(restoreButton, 1, 1);

        statusLabel.Dock = DockStyle.Fill;
        statusLabel.Height = 42;
        statusLabel.TextAlign = ContentAlignment.MiddleLeft;

        root.Controls.Add(header, 0, 0);
        root.Controls.Add(infoPanel, 0, 1);
        root.Controls.Add(pickerPanel, 0, 2);
        root.Controls.Add(buttons, 0, 3);
        root.Controls.Add(statusLabel, 0, 4);
        Controls.Add(root);
    }

    private Control CreateBranchRow()
    {
        var row = CreateTwoColumnRow(128);
        branchBox.DropDownStyle = ComboBoxStyle.DropDown;
        branchBox.BackColor = Color.FromArgb(20, 22, 24);
        branchBox.ForeColor = AppTheme.Text;
        branchBox.FlatStyle = FlatStyle.Flat;
        branchBox.Font = AppTheme.MainFont(11F);
        branchBox.Dock = DockStyle.Fill;
        branchBox.Margin = new Padding(0, 5, 10, 5);
        branchBox.TextChanged += (_, _) => RefreshModeLabel();

        refreshBranchesButton.Width = 118;
        refreshBranchesButton.Height = 38;
        refreshBranchesButton.Click += async (_, _) => await LoadBranchesAsync();

        row.Controls.Add(branchBox, 0, 0);
        row.Controls.Add(refreshBranchesButton, 1, 0);
        return row;
    }

    private Control CreateCharacterRow()
    {
        var row = CreateTwoColumnRow(128);
        characterBox.DropDownStyle = ComboBoxStyle.DropDownList;
        characterBox.BackColor = Color.FromArgb(20, 22, 24);
        characterBox.ForeColor = AppTheme.Text;
        characterBox.FlatStyle = FlatStyle.Flat;
        characterBox.Font = AppTheme.MainFont(11F);
        characterBox.Dock = DockStyle.Fill;
        characterBox.Margin = new Padding(0, 5, 10, 5);
        characterBox.SelectedIndexChanged += (_, _) => RefreshModeLabel();

        refreshCharactersButton.Width = 118;
        refreshCharactersButton.Height = 38;
        refreshCharactersButton.Click += (_, _) => LoadCharacters();

        row.Controls.Add(characterBox, 0, 0);
        row.Controls.Add(refreshCharactersButton, 1, 0);
        return row;
    }

    private static TableLayoutPanel CreatePanel(int height)
    {
        return new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            ColumnCount = 1,
            Height = height,
            Padding = new Padding(16),
            Margin = new Padding(0, 0, 0, 12),
            BackColor = AppTheme.Surface
        };
    }

    private static TableLayoutPanel CreateTwoColumnRow(int rightColumnWidth)
    {
        var row = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 1,
            BackColor = AppTheme.Surface,
            Margin = new Padding(0)
        };
        row.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        row.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, rightColumnWidth));
        return row;
    }

    private static Label CreateInfoLabel(Color? color = null, FontStyle style = FontStyle.Regular)
    {
        return new Label
        {
            AutoSize = false,
            Dock = DockStyle.Top,
            Height = 24,
            AutoEllipsis = true,
            ForeColor = color ?? AppTheme.MutedText,
            Font = AppTheme.MainFont(9.5F, style),
            TextAlign = ContentAlignment.MiddleLeft
        };
    }

    private async Task RunSaveAllAsync()
    {
        if (!ValidateReadyForOperations(out var token))
        {
            return;
        }

        if (processGuard.IsGameRunning(out var processes))
        {
            ShowError($"Close Minecraft Dungeons first. Running process: {processes}");
            return;
        }

        await RunBusyAsync("Creating full save archive...", async cancellationToken =>
        {
            var archivePath = Path.Combine(Path.GetTempPath(), $"mcd-backup-{Guid.NewGuid():N}.zip");
            try
            {
                var manifest = await archiveService.CreateBackupArchiveAsync(settings.LocalSaveProfilePath, archivePath, cancellationToken);
                using var httpClient = new HttpClient();
                var client = new GitHubContentClient(httpClient, settings, token);
                await client.UploadFileAsync(archivePath, settings.RemotePath, settings.Branch, $"Minecraft Dungeons full backup {DateTimeOffset.Now:yyyy-MM-dd HH:mm:ss}", cancellationToken);
                SelectBranch(settings.Branch);
                SetStatus($"Full backup uploaded to {settings.Branch}. Files: {manifest.Files.Count}.");
            }
            finally
            {
                TryDelete(archivePath);
            }
        });
    }

    private async Task RunPushCharacterAsync()
    {
        if (!ValidateReadyForOperations(out var token))
        {
            return;
        }

        if (GetSelectedCharacter() is not { } character)
        {
            ShowError("Select a character .dat file first.");
            return;
        }

        if (processGuard.IsGameRunning(out var processes))
        {
            ShowError($"Close Minecraft Dungeons first. Running process: {processes}");
            return;
        }

        var branch = ResolveCharacterBranch(character);
        var remotePath = CharacterFile.CreateRemotePath(character.FileName);

        await RunBusyAsync($"Uploading {character.FileName} to {branch}...", async cancellationToken =>
        {
            using var httpClient = new HttpClient();
            var client = new GitHubContentClient(httpClient, settings, token);
            await client.UploadCharacterFileAsync(
                character.FullPath,
                branch,
                remotePath,
                $"Minecraft Dungeons character backup {character.FileName} {DateTimeOffset.Now:yyyy-MM-dd HH:mm:ss}",
                cancellationToken);
            AddBranchIfMissing(branch);
            SelectBranch(branch);
            SetStatus($"Character uploaded. Branch: {branch}. File: {remotePath}");
        });
    }

    private async Task RunRestoreCurrentBranchAsync()
    {
        var branch = GetSelectedBranch();
        if (string.Equals(branch, settings.Branch, StringComparison.OrdinalIgnoreCase))
        {
            await RunRestoreFullBackupAsync();
            return;
        }

        await RunRestoreCharacterAsync(branch);
    }

    private async Task RunRestoreFullBackupAsync()
    {
        if (!ValidateReadyForOperations(out var token))
        {
            return;
        }

        if (processGuard.IsGameRunning(out var processes))
        {
            ShowError($"Close Minecraft Dungeons first. Running process: {processes}");
            return;
        }

        var confirm = MessageBox.Show(
            this,
            "This restores the full main backup. Current local saves will be backed up first.",
            "Restore full save",
            MessageBoxButtons.OKCancel,
            MessageBoxIcon.Warning);

        if (confirm != DialogResult.OK)
        {
            return;
        }

        await RunBusyAsync("Downloading full backup...", async cancellationToken =>
        {
            var archivePath = Path.Combine(Path.GetTempPath(), $"mcd-restore-{Guid.NewGuid():N}.zip");
            try
            {
                using var httpClient = new HttpClient();
                var client = new GitHubContentClient(httpClient, settings, token);
                await client.DownloadFileAsync(settings.RemotePath, settings.Branch, archivePath, cancellationToken);

                var result = await archiveService.RestoreArchiveAsync(
                    archivePath,
                    settings.LocalSaveProfilePath,
                    settingsStore.GetPreRestoreBackupDirectory(),
                    cancellationToken);

                LoadCharacters();
                SetStatus($"Full restore complete. Files: {result.Manifest.Files.Count}.");
            }
            finally
            {
                TryDelete(archivePath);
            }
        });
    }

    private async Task RunRestoreCharacterAsync(string branch)
    {
        if (!ValidateReadyForOperations(out var token))
        {
            return;
        }

        if (GetSelectedCharacter() is not { } character)
        {
            ShowError("Select the local .dat character to replace.");
            return;
        }

        if (processGuard.IsGameRunning(out var processes))
        {
            ShowError($"Close Minecraft Dungeons first. Running process: {processes}");
            return;
        }

        var confirm = MessageBox.Show(
            this,
            $"This restores {character.FileName} from branch {branch}. The current local .dat will be backed up first.",
            "Restore character",
            MessageBoxButtons.OKCancel,
            MessageBoxIcon.Warning);

        if (confirm != DialogResult.OK)
        {
            return;
        }

        await RunBusyAsync($"Downloading character from {branch}...", async cancellationToken =>
        {
            var tempPath = Path.Combine(Path.GetTempPath(), $"mcd-character-{Guid.NewGuid():N}.dat");
            try
            {
                var remotePath = CharacterFile.CreateRemotePath(character.FileName);
                using var httpClient = new HttpClient();
                var client = new GitHubContentClient(httpClient, settings, token);
                await client.DownloadFileAsync(remotePath, branch, tempPath, cancellationToken);

                var targetPath = Path.Combine(settings.LocalSaveProfilePath, "Characters", character.FileName);
                Directory.CreateDirectory(Path.GetDirectoryName(targetPath)!);

                if (File.Exists(targetPath))
                {
                    var backupDirectory = Path.Combine(settingsStore.GetPreRestoreBackupDirectory(), "characters");
                    Directory.CreateDirectory(backupDirectory);
                    var backupPath = Path.Combine(
                        backupDirectory,
                        $"{Path.GetFileNameWithoutExtension(character.FileName)}-{DateTimeOffset.Now:yyyyMMdd-HHmmss}.dat");
                    File.Copy(targetPath, backupPath, overwrite: false);
                }

                File.Copy(tempPath, targetPath, overwrite: true);
                LoadCharacters();
                SetStatus($"{character.FileName} restored from {branch}.");
            }
            finally
            {
                TryDelete(tempPath);
            }
        });
    }

    private async Task LoadBranchesAsync()
    {
        if (!ValidateReadyForGitHub(out var token))
        {
            return;
        }

        await RunBusyAsync("Loading branches...", async cancellationToken =>
        {
            using var httpClient = new HttpClient();
            var client = new GitHubContentClient(httpClient, settings, token);
            var currentBranch = GetSelectedBranch();
            var branches = await client.ListBranchesAsync(cancellationToken);
            SetBranchItems(branches);
            SelectBranch(branches.Contains(currentBranch, StringComparer.OrdinalIgnoreCase) ? currentBranch : settings.Branch);
            SetStatus($"Branches loaded: {branches.Count}.");
        });
    }

    private void OpenSettings()
    {
        using var form = new SettingsForm(settings, settingsStore, credentialStore);
        if (form.ShowDialog(this) == DialogResult.OK)
        {
            settings = settingsStore.Load();
            RefreshState();
            SetStatus("Settings saved.");
        }
    }

    private bool ValidateReadyForGitHub(out string token)
    {
        settings = settingsStore.Load();
        token = credentialStore.ReadToken() ?? "";

        var errors = settings.ValidateForGitHub().ToList();
        if (string.IsNullOrWhiteSpace(token))
        {
            errors.Add("GitHub token is required. Open Settings and save a token.");
        }

        if (errors.Count == 0)
        {
            return true;
        }

        ShowError(string.Join(Environment.NewLine, errors));
        return false;
    }

    private bool ValidateReadyForOperations(out string token)
    {
        if (!ValidateReadyForGitHub(out token))
        {
            return false;
        }

        var errors = settings.ValidateForSaveOperations().Except(settings.ValidateForGitHub()).ToList();
        if (errors.Count == 0)
        {
            return true;
        }

        ShowError(string.Join(Environment.NewLine, errors));
        return false;
    }

    private async Task RunBusyAsync(string initialStatus, Func<CancellationToken, Task> action)
    {
        ToggleButtons(false);
        SetStatus(initialStatus);

        using var cancellation = new CancellationTokenSource();
        try
        {
            await action(cancellation.Token);
        }
        catch (Exception ex)
        {
            ShowError(ex.Message);
        }
        finally
        {
            ToggleButtons(true);
        }
    }

    private void ToggleButtons(bool enabled)
    {
        saveAllButton.Enabled = enabled;
        pushCharacterButton.Enabled = enabled;
        restoreButton.Enabled = enabled;
        settingsButton.Enabled = enabled;
        refreshCharactersButton.Enabled = enabled;
        refreshBranchesButton.Enabled = enabled;
    }

    private void RefreshState()
    {
        profileLabel.Text = string.IsNullOrWhiteSpace(settings.LocalSaveProfilePath)
            ? "Save profile: not selected"
            : $"Save profile: {settings.LocalSaveProfilePath}";
        remoteLabel.Text = string.IsNullOrWhiteSpace(settings.Owner) || string.IsNullOrWhiteSpace(settings.Repo)
            ? "GitHub: not configured"
            : $"GitHub: {settings.Owner}/{settings.Repo}@{settings.Branch} / {settings.RemotePath}";
        SetBranchItems([settings.Branch]);
        SelectBranch(string.IsNullOrWhiteSpace(branchBox.Text) ? settings.Branch : branchBox.Text);
        LoadCharacters();
        statusLabel.Text = "Ready.";
    }

    private void LoadCharacters()
    {
        characterBox.BeginUpdate();
        try
        {
            characterBox.DataSource = null;
            characters = Directory.Exists(settings.LocalSaveProfilePath)
                ? CharacterFile.FindInProfile(settings.LocalSaveProfilePath)
                : [];
            characterBox.DataSource = characters.ToList();
            characterBox.DisplayMember = nameof(CharacterFile.DisplayName);
            if (characters.Count > 0)
            {
                characterBox.SelectedIndex = 0;
            }
        }
        finally
        {
            characterBox.EndUpdate();
        }

        AddSuggestedBranchForSelectedCharacter();
        RefreshModeLabel();
    }

    private void SetBranchItems(IReadOnlyList<string> branches)
    {
        var currentText = branchBox.Text;
        branchBox.BeginUpdate();
        try
        {
            branchBox.Items.Clear();
            foreach (var branch in branches.Distinct(StringComparer.OrdinalIgnoreCase))
            {
                branchBox.Items.Add(branch);
            }
        }
        finally
        {
            branchBox.EndUpdate();
        }

        branchBox.Text = string.IsNullOrWhiteSpace(currentText) ? settings.Branch : currentText;
        AddSuggestedBranchForSelectedCharacter();
    }

    private void SelectBranch(string branch)
    {
        branchBox.Text = string.IsNullOrWhiteSpace(branch) ? settings.Branch : branch;
        RefreshModeLabel();
    }

    private void AddSuggestedBranchForSelectedCharacter()
    {
        if (GetSelectedCharacter() is not { } character)
        {
            return;
        }

        AddBranchIfMissing(CharacterFile.CreateBranchName(character.FileName));
    }

    private void AddBranchIfMissing(string branch)
    {
        if (!branchBox.Items.Cast<object>().Any(item => string.Equals(item.ToString(), branch, StringComparison.OrdinalIgnoreCase)))
        {
            branchBox.Items.Add(branch);
        }
    }

    private string GetSelectedBranch()
    {
        return string.IsNullOrWhiteSpace(branchBox.Text) ? settings.Branch : branchBox.Text.Trim();
    }

    private string ResolveCharacterBranch(CharacterFile character)
    {
        var branch = GetSelectedBranch();
        return string.Equals(branch, settings.Branch, StringComparison.OrdinalIgnoreCase)
            ? CharacterFile.CreateBranchName(character.FileName)
            : branch;
    }

    private CharacterFile? GetSelectedCharacter()
    {
        return characterBox.SelectedItem as CharacterFile;
    }

    private void RefreshModeLabel()
    {
        var branch = GetSelectedBranch();
        if (GetSelectedCharacter() is { } character)
        {
            AddBranchIfMissing(CharacterFile.CreateBranchName(character.FileName));
        }

        if (string.Equals(branch, settings.Branch, StringComparison.OrdinalIgnoreCase))
        {
            var suggestion = GetSelectedCharacter() is { } selected
                ? $" Save character will use/create {CharacterFile.CreateBranchName(selected.FileName)}."
                : "";
            modeLabel.Text = $"Mode: full profile on {settings.Branch}.{suggestion}";
            return;
        }

        modeLabel.Text = GetSelectedCharacter() is { } characterFile
            ? $"Mode: character branch {branch} -> {CharacterFile.CreateRemotePath(characterFile.FileName)}"
            : $"Mode: character branch {branch}. Select a .dat file.";
    }

    private void ShowError(string message)
    {
        SetStatus(message);
        MessageBox.Show(this, message, "Minecraft Dungeons Character Saver", MessageBoxButtons.OK, MessageBoxIcon.Error);
    }

    private void SetStatus(string message)
    {
        if (InvokeRequired)
        {
            BeginInvoke(() => SetStatus(message));
            return;
        }

        statusLabel.Text = message;
    }

    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch
        {
            // Temp cleanup should not hide the real operation result.
        }
    }
}
