using McDungeonsGitBackup.Core;

namespace McDungeonsGitBackup.App;

public sealed class MainForm : Form
{
    private readonly SettingsStore settingsStore;
    private readonly ICredentialStore credentialStore;
    private readonly ArchiveService archiveService = new();
    private readonly GameProcessGuard processGuard = new();
    private readonly Button saveAllButton;
    private readonly Button pushCharacterButton;
    private readonly Button restoreButton;
    private readonly Button settingsButton;
    private readonly Button refreshCharactersButton;
    private readonly Button refreshBranchesButton;
    private readonly Label profileLabel = CreateInfoLabel();
    private readonly Label remoteLabel = CreateInfoLabel();
    private readonly Label branchInfoLabel = CreateInfoLabel(AppTheme.Gold, FontStyle.Bold);
    private readonly Label statusLabel = CreateInfoLabel(AppTheme.MutedText);
    private readonly ComboBox characterBox = new();
    private readonly ComboBox branchBox = new();

    private AppSettings settings;
    private IReadOnlyList<CharacterFile> characters = [];

    public MainForm(SettingsStore settingsStore, ICredentialStore credentialStore)
    {
        this.settingsStore = settingsStore;
        this.credentialStore = credentialStore;
        settings = settingsStore.Load();

        saveAllButton = AppTheme.Button("Сохранить всё в main", AppTheme.Emerald);
        settingsButton = AppTheme.Button("Настройки", AppTheme.Border);
        pushCharacterButton = AppTheme.Button("Сохранить выбранного персонажа", AppTheme.Gold);
        restoreButton = AppTheme.Button("Восстановить из текущей ветки", AppTheme.Redstone);
        refreshCharactersButton = AppTheme.Button("Обновить", AppTheme.Border);
        refreshBranchesButton = AppTheme.Button("Обновить ветки", AppTheme.Border);

        Text = "Minecraft Dungeons Git Backup";
        Width = 1120;
        Height = 760;
        MinimumSize = new Size(980, 680);
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
            RowCount = 6,
            Padding = new Padding(32),
            BackColor = AppTheme.Background,
            AutoScroll = true
        };
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        var title = AppTheme.Label("Minecraft Dungeons Backup", 28F, AppTheme.Text, FontStyle.Bold);
        var subtitle = AppTheme.Label("Выбери ветку: main работает со всем профилем, остальные ветки - с выбранным персонажем.", 12F, AppTheme.MutedText);

        var header = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            AutoSize = true,
            Margin = new Padding(0, 0, 0, 22),
            BackColor = AppTheme.Background
        };
        header.Controls.Add(title);
        header.Controls.Add(subtitle);

        var infoPanel = CreatePanel();
        infoPanel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        infoPanel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        infoPanel.Controls.Add(profileLabel, 0, 0);
        infoPanel.Controls.Add(remoteLabel, 0, 1);

        var branchPanel = CreatePanel();
        branchPanel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        branchPanel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        branchPanel.Controls.Add(AppTheme.Label("Текущая ветка", 17F, AppTheme.Text, FontStyle.Bold), 0, 0);

        var branchRow = CreateTwoColumnRow(190);
        branchBox.DropDownStyle = ComboBoxStyle.DropDown;
        branchBox.BackColor = Color.FromArgb(20, 22, 24);
        branchBox.ForeColor = AppTheme.Text;
        branchBox.FlatStyle = FlatStyle.Flat;
        branchBox.Font = AppTheme.MainFont(12F);
        branchBox.Dock = DockStyle.Fill;
        branchBox.Height = 42;
        branchBox.Margin = new Padding(0, 6, 14, 0);
        branchBox.TextChanged += (_, _) => RefreshBranchInfo();

        refreshBranchesButton.Width = 176;
        refreshBranchesButton.Click += async (_, _) => await LoadBranchesAsync();

        branchRow.Controls.Add(branchBox, 0, 0);
        branchRow.Controls.Add(refreshBranchesButton, 1, 0);
        branchPanel.Controls.Add(branchRow, 0, 1);

        var characterPanel = CreatePanel();
        characterPanel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        characterPanel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        characterPanel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        characterPanel.Controls.Add(AppTheme.Label("Выбранный персонаж", 17F, AppTheme.Text, FontStyle.Bold), 0, 0);

        var characterRow = CreateTwoColumnRow(150);
        characterBox.DropDownStyle = ComboBoxStyle.DropDownList;
        characterBox.BackColor = Color.FromArgb(20, 22, 24);
        characterBox.ForeColor = AppTheme.Text;
        characterBox.FlatStyle = FlatStyle.Flat;
        characterBox.Font = AppTheme.MainFont(12F);
        characterBox.Dock = DockStyle.Fill;
        characterBox.Height = 42;
        characterBox.Margin = new Padding(0, 6, 14, 0);
        characterBox.SelectedIndexChanged += (_, _) => RefreshBranchInfo();

        refreshCharactersButton.Width = 136;
        refreshCharactersButton.Click += (_, _) => LoadCharacters();

        characterRow.Controls.Add(characterBox, 0, 0);
        characterRow.Controls.Add(refreshCharactersButton, 1, 0);

        branchInfoLabel.Margin = new Padding(0, 14, 0, 0);
        branchInfoLabel.Height = 56;
        characterPanel.Controls.Add(characterRow, 0, 1);
        characterPanel.Controls.Add(branchInfoLabel, 0, 2);

        var buttons = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 2,
            AutoSize = false,
            Height = 150,
            BackColor = AppTheme.Background,
            Margin = new Padding(0, 18, 0, 18)
        };
        buttons.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        buttons.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        buttons.RowStyles.Add(new RowStyle(SizeType.Percent, 50));
        buttons.RowStyles.Add(new RowStyle(SizeType.Percent, 50));

        saveAllButton.Dock = DockStyle.Fill;
        settingsButton.Dock = DockStyle.Fill;
        pushCharacterButton.Dock = DockStyle.Fill;
        restoreButton.Dock = DockStyle.Fill;
        saveAllButton.Margin = new Padding(0, 0, 10, 10);
        settingsButton.Margin = new Padding(0, 10, 10, 0);
        pushCharacterButton.Margin = new Padding(10, 0, 0, 10);
        restoreButton.Margin = new Padding(10, 10, 0, 0);

        saveAllButton.Click += async (_, _) => await RunSaveAllAsync();
        settingsButton.Click += (_, _) => OpenSettings();
        pushCharacterButton.Click += async (_, _) => await RunPushCharacterAsync();
        restoreButton.Click += async (_, _) => await RunRestoreCurrentBranchAsync();

        buttons.Controls.Add(saveAllButton, 0, 0);
        buttons.Controls.Add(settingsButton, 0, 1);
        buttons.Controls.Add(pushCharacterButton, 1, 0);
        buttons.Controls.Add(restoreButton, 1, 1);

        statusLabel.Dock = DockStyle.Fill;
        statusLabel.Height = 56;
        statusLabel.TextAlign = ContentAlignment.MiddleLeft;

        root.Controls.Add(header, 0, 0);
        root.Controls.Add(infoPanel, 0, 1);
        root.Controls.Add(branchPanel, 0, 2);
        root.Controls.Add(characterPanel, 0, 3);
        root.Controls.Add(buttons, 0, 4);
        root.Controls.Add(statusLabel, 0, 5);
        Controls.Add(root);
    }

    private static TableLayoutPanel CreatePanel()
    {
        return new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            ColumnCount = 1,
            AutoSize = true,
            Padding = new Padding(24),
            Margin = new Padding(0, 0, 0, 18),
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
            AutoSize = false,
            Height = 56,
            BackColor = AppTheme.Surface,
            Margin = new Padding(0, 14, 0, 0)
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
            Height = 28,
            AutoEllipsis = true,
            ForeColor = color ?? AppTheme.MutedText,
            Font = AppTheme.MainFont(10.5F, style),
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
            ShowError($"Сначала закрой Minecraft Dungeons. Запущен процесс: {processes}");
            return;
        }

        await RunBusyAsync("Создаю полный архив сейва...", async cancellationToken =>
        {
            var archivePath = Path.Combine(Path.GetTempPath(), $"mcd-backup-{Guid.NewGuid():N}.zip");
            try
            {
                var manifest = await archiveService.CreateBackupArchiveAsync(settings.LocalSaveProfilePath, archivePath, cancellationToken);
                SetStatus($"Загружаю полный сейв в {settings.Branch}...");
                using var httpClient = new HttpClient();
                var client = new GitHubContentClient(httpClient, settings, token);
                await client.UploadFileAsync(archivePath, settings.RemotePath, settings.Branch, $"Minecraft Dungeons full backup {DateTimeOffset.Now:yyyy-MM-dd HH:mm:ss}", cancellationToken);
                SelectBranch(settings.Branch);
                SetStatus($"Готово: полный бэкап загружен в {settings.Branch}. Файлов: {manifest.Files.Count}.");
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
            ShowError("Сначала выбери .dat персонажа.");
            return;
        }

        if (processGuard.IsGameRunning(out var processes))
        {
            ShowError($"Сначала закрой Minecraft Dungeons. Запущен процесс: {processes}");
            return;
        }

        var branch = GetSelectedBranch();
        if (string.Equals(branch, settings.Branch, StringComparison.OrdinalIgnoreCase))
        {
            ShowError($"Для выбранного персонажа выбери или впиши отдельную ветку, например {CharacterFile.CreateBranchName(character.FileName)}. Ветка {settings.Branch} хранит полный архив.");
            return;
        }

        var remotePath = CharacterFile.CreateRemotePath(character.FileName);

        await RunBusyAsync($"Загружаю {character.FileName} в {branch}...", async cancellationToken =>
        {
            using var httpClient = new HttpClient();
            var client = new GitHubContentClient(httpClient, settings, token);
            await client.UploadCharacterFileAsync(
                character.FullPath,
                branch,
                remotePath,
                $"Minecraft Dungeons character backup {character.FileName} {DateTimeOffset.Now:yyyy-MM-dd HH:mm:ss}",
                cancellationToken);
            SelectBranch(branch);
            SetStatus($"Готово: персонаж загружен. Ветка: {branch}. Файл: {remotePath}");
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
            ShowError($"Сначала закрой Minecraft Dungeons. Запущен процесс: {processes}");
            return;
        }

        var confirm = MessageBox.Show(
            this,
            "Будет восстановлен полный архив из main. Текущая папка сейва сначала будет сохранена в локальный pre-restore backup.",
            "Восстановить полный сейв",
            MessageBoxButtons.OKCancel,
            MessageBoxIcon.Warning);

        if (confirm != DialogResult.OK)
        {
            return;
        }

        await RunBusyAsync("Скачиваю полный архив из GitHub...", async cancellationToken =>
        {
            var archivePath = Path.Combine(Path.GetTempPath(), $"mcd-restore-{Guid.NewGuid():N}.zip");
            try
            {
                using var httpClient = new HttpClient();
                var client = new GitHubContentClient(httpClient, settings, token);
                await client.DownloadFileAsync(settings.RemotePath, settings.Branch, archivePath, cancellationToken);

                SetStatus("Проверяю архив и восстанавливаю сейвы...");
                var result = await archiveService.RestoreArchiveAsync(
                    archivePath,
                    settings.LocalSaveProfilePath,
                    settingsStore.GetPreRestoreBackupDirectory(),
                    cancellationToken);

                LoadCharacters();
                var backupText = result.PreRestoreBackupPath is null
                    ? "Локальных файлов до восстановления не было."
                    : $"Локальный pre-restore backup: {result.PreRestoreBackupPath}";
                SetStatus($"Готово: восстановлено файлов: {result.Manifest.Files.Count}. {backupText}");
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
            ShowError("Сначала выбери локальный .dat персонажа, который нужно заменить.");
            return;
        }

        if (processGuard.IsGameRunning(out var processes))
        {
            ShowError($"Сначала закрой Minecraft Dungeons. Запущен процесс: {processes}");
            return;
        }

        var confirm = MessageBox.Show(
            this,
            $"Будет восстановлен персонаж {character.FileName} из ветки {branch}. Текущий локальный .dat будет сохранен рядом с pre-restore backups.",
            "Восстановить персонажа",
            MessageBoxButtons.OKCancel,
            MessageBoxIcon.Warning);

        if (confirm != DialogResult.OK)
        {
            return;
        }

        await RunBusyAsync($"Скачиваю персонажа из {branch}...", async cancellationToken =>
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
                SetStatus($"Готово: {character.FileName} восстановлен из ветки {branch}.");
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

        await RunBusyAsync("Загружаю список веток...", async cancellationToken =>
        {
            using var httpClient = new HttpClient();
            var client = new GitHubContentClient(httpClient, settings, token);
            var currentBranch = GetSelectedBranch();
            var branches = await client.ListBranchesAsync(cancellationToken);
            SetBranchItems(branches);
            SelectBranch(branches.Contains(currentBranch, StringComparer.OrdinalIgnoreCase) ? currentBranch : settings.Branch);
            SetStatus($"Ветки обновлены: {branches.Count}.");
        });
    }

    private void OpenSettings()
    {
        using var form = new SettingsForm(settings, settingsStore, credentialStore);
        if (form.ShowDialog(this) == DialogResult.OK)
        {
            settings = settingsStore.Load();
            RefreshState();
            SetStatus("Настройки сохранены.");
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
            ? "Профиль сейва: не выбран"
            : $"Профиль сейва: {settings.LocalSaveProfilePath}";
        remoteLabel.Text = string.IsNullOrWhiteSpace(settings.Owner) || string.IsNullOrWhiteSpace(settings.Repo)
            ? "GitHub: не настроен"
            : $"GitHub: {settings.Owner}/{settings.Repo}@{settings.Branch} / {settings.RemotePath}";
        SetBranchItems([settings.Branch]);
        SelectBranch(string.IsNullOrWhiteSpace(branchBox.Text) ? settings.Branch : branchBox.Text);
        LoadCharacters();
        statusLabel.Text = "Готово к работе.";
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

        RefreshBranchInfo();
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
    }

    private void SelectBranch(string branch)
    {
        branchBox.Text = string.IsNullOrWhiteSpace(branch) ? settings.Branch : branch;
        RefreshBranchInfo();
    }

    private string GetSelectedBranch()
    {
        return string.IsNullOrWhiteSpace(branchBox.Text) ? settings.Branch : branchBox.Text.Trim();
    }

    private CharacterFile? GetSelectedCharacter()
    {
        return characterBox.SelectedItem as CharacterFile;
    }

    private void RefreshBranchInfo()
    {
        var branch = GetSelectedBranch();
        if (string.Equals(branch, settings.Branch, StringComparison.OrdinalIgnoreCase))
        {
            branchInfoLabel.Text = $"Текущий режим: полный профиль. Restore восстановит архив {settings.RemotePath} из {settings.Branch}.";
            return;
        }

        if (GetSelectedCharacter() is not { } character)
        {
            branchInfoLabel.Text = $"Текущий режим: ветка {branch}. Выбери .dat персонажа для save/restore.";
            return;
        }

        branchInfoLabel.Text = $"Текущий режим: персонаж. Ветка: {branch} | Файл: {CharacterFile.CreateRemotePath(character.FileName)}";
    }

    private void ShowError(string message)
    {
        SetStatus(message);
        MessageBox.Show(this, message, "Minecraft Dungeons Git Backup", MessageBoxButtons.OK, MessageBoxIcon.Error);
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
