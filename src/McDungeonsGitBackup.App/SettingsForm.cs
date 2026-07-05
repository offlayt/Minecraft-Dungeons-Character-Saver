using McDungeonsGitBackup.Core;

namespace McDungeonsGitBackup.App;

public sealed class SettingsForm : Form
{
    private readonly SettingsStore settingsStore;
    private readonly ICredentialStore credentialStore;
    private readonly TextBox ownerBox = new();
    private readonly TextBox repoBox = new();
    private readonly TextBox branchBox = new();
    private readonly TextBox remotePathBox = new();
    private readonly TextBox localPathBox = new();
    private readonly TextBox tokenBox = new();
    private readonly Label statusLabel = new();

    public SettingsForm(AppSettings settings, SettingsStore settingsStore, ICredentialStore credentialStore)
    {
        this.settingsStore = settingsStore;
        this.credentialStore = credentialStore;

        Text = "Settings";
        Width = 980;
        Height = 560;
        MinimumSize = new Size(900, 520);
        StartPosition = FormStartPosition.CenterParent;
        AppTheme.StyleForm(this);

        ownerBox.Text = settings.Owner;
        repoBox.Text = settings.Repo;
        branchBox.Text = settings.Branch;
        remotePathBox.Text = settings.RemotePath;
        localPathBox.Text = settings.LocalSaveProfilePath;
        tokenBox.UseSystemPasswordChar = true;
        tokenBox.PlaceholderText = credentialStore.ReadToken() is null ? "GitHub token" : "Leave blank to keep saved token";

        BuildUi();
    }

    private void BuildUi()
    {
        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(28),
            ColumnCount = 3,
            RowCount = 8,
            BackColor = AppTheme.Background
        };

        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 170));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 150));

        AddRow(layout, 0, "GitHub owner", ownerBox);
        AddRow(layout, 1, "Repository", repoBox);
        AddRow(layout, 2, "Main branch", branchBox);
        AddRow(layout, 3, "GitHub path", remotePathBox);
        AddRow(layout, 4, "Save folder", localPathBox, CreateButton("Browse", BrowseLocalPath));
        AddRow(layout, 5, "GitHub token", tokenBox);

        var autoDetectButton = CreateButton("Auto-detect", AutoDetectSaves);
        var testButton = CreateButton("Test GitHub", async () => await TestConnectionAsync());
        var saveButton = CreateButton("Save", SaveSettings);
        var cancelButton = CreateButton("Cancel", () => DialogResult = DialogResult.Cancel);

        var buttonPanel = new FlowLayoutPanel
        {
            FlowDirection = FlowDirection.LeftToRight,
            Dock = DockStyle.Fill,
            AutoSize = true,
            BackColor = AppTheme.Background,
            Margin = new Padding(0, 18, 0, 0)
        };
        buttonPanel.Controls.Add(autoDetectButton);
        buttonPanel.Controls.Add(testButton);
        buttonPanel.Controls.Add(saveButton);
        buttonPanel.Controls.Add(cancelButton);

        statusLabel.AutoSize = false;
        statusLabel.Dock = DockStyle.Fill;
        statusLabel.Height = 70;
        statusLabel.TextAlign = ContentAlignment.MiddleLeft;
        statusLabel.ForeColor = AppTheme.MutedText;
        statusLabel.Font = AppTheme.MainFont(9F);

        layout.Controls.Add(buttonPanel, 1, 6);
        layout.SetColumnSpan(buttonPanel, 2);
        layout.Controls.Add(statusLabel, 0, 7);
        layout.SetColumnSpan(statusLabel, 3);

        Controls.Add(layout);
    }

    private static void AddRow(TableLayoutPanel layout, int row, string label, TextBox box, Control? extra = null)
    {
        box.Dock = DockStyle.Fill;
        box.Height = 38;
        box.Font = AppTheme.MainFont(11F);
        box.Margin = new Padding(0, 7, 12, 7);
        box.BackColor = Color.FromArgb(20, 22, 24);
        box.ForeColor = AppTheme.Text;
        box.BorderStyle = BorderStyle.FixedSingle;

        var labelControl = new Label
        {
            Text = label,
            AutoSize = true,
            Anchor = AnchorStyles.Left,
            TextAlign = ContentAlignment.MiddleLeft,
            ForeColor = AppTheme.MutedText,
            Font = AppTheme.MainFont(10.5F, FontStyle.Bold),
            Margin = new Padding(0, 8, 0, 8)
        };

        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 52));
        layout.Controls.Add(labelControl, 0, row);
        layout.Controls.Add(box, 1, row);

        if (extra is not null)
        {
            layout.Controls.Add(extra, 2, row);
        }
    }

    private static Button CreateButton(string text, Action action)
    {
        var button = new Button
        {
            Text = text,
            AutoSize = false,
            Width = 136,
            Height = 42,
            Margin = new Padding(4, 5, 4, 5),
            FlatStyle = FlatStyle.Flat,
            BackColor = AppTheme.SurfaceAlt,
            ForeColor = AppTheme.Text,
            Font = AppTheme.MainFont(10F, FontStyle.Bold)
        };
        button.FlatAppearance.BorderColor = AppTheme.Border;
        button.Click += (_, _) => action();
        return button;
    }

    private static Button CreateButton(string text, Func<Task> action)
    {
        var button = new Button
        {
            Text = text,
            AutoSize = false,
            Width = 150,
            Height = 42,
            Margin = new Padding(4, 5, 4, 5),
            FlatStyle = FlatStyle.Flat,
            BackColor = AppTheme.SurfaceAlt,
            ForeColor = AppTheme.Text,
            Font = AppTheme.MainFont(10F, FontStyle.Bold)
        };
        button.FlatAppearance.BorderColor = AppTheme.Border;
        button.Click += async (_, _) => await action();
        return button;
    }

    private void BrowseLocalPath()
    {
        using var dialog = new FolderBrowserDialog
        {
            Description = "Select the Minecraft Dungeons save profile folder",
            UseDescriptionForTitle = true,
            SelectedPath = Directory.Exists(localPathBox.Text) ? localPathBox.Text : ""
        };

        if (dialog.ShowDialog(this) == DialogResult.OK)
        {
            localPathBox.Text = dialog.SelectedPath;
        }
    }

    private void AutoDetectSaves()
    {
        var profiles = new SaveLocator().FindProfiles();
        if (profiles.Count == 0)
        {
            SetStatus("No save profile found. Select the folder manually.");
            BrowseLocalPath();
            return;
        }

        if (profiles.Count == 1)
        {
            localPathBox.Text = profiles[0].Path;
            SetStatus("Save profile detected.");
            return;
        }

        using var picker = new ProfilePickerForm(profiles);
        if (picker.ShowDialog(this) == DialogResult.OK && picker.SelectedProfile is not null)
        {
            localPathBox.Text = picker.SelectedProfile.Path;
            SetStatus("Save profile selected.");
        }
    }

    private async Task TestConnectionAsync()
    {
        var settings = ReadSettings();
        var errors = settings.ValidateForGitHub();
        if (errors.Count > 0)
        {
            ShowError(string.Join(Environment.NewLine, errors));
            return;
        }

        var token = string.IsNullOrWhiteSpace(tokenBox.Text) ? credentialStore.ReadToken() : tokenBox.Text;
        if (string.IsNullOrWhiteSpace(token))
        {
            ShowError("GitHub token is required.");
            return;
        }

        try
        {
            SetStatus("Testing GitHub connection...");
            using var httpClient = new HttpClient();
            var client = new GitHubContentClient(httpClient, settings, token);
            await client.TestConnectionAsync();
            SetStatus("GitHub connection works.");
        }
        catch (Exception ex)
        {
            ShowError(ex.Message);
        }
    }

    private void SaveSettings()
    {
        var settings = ReadSettings();
        var errors = settings.ValidateForSaveOperations().ToList();
        var existingToken = credentialStore.ReadToken();

        if (string.IsNullOrWhiteSpace(tokenBox.Text) && string.IsNullOrWhiteSpace(existingToken))
        {
            errors.Add("GitHub token is required.");
        }

        if (errors.Count > 0)
        {
            ShowError(string.Join(Environment.NewLine, errors));
            return;
        }

        settingsStore.Save(settings);
        if (!string.IsNullOrWhiteSpace(tokenBox.Text))
        {
            credentialStore.SaveToken(tokenBox.Text);
        }

        DialogResult = DialogResult.OK;
    }

    private AppSettings ReadSettings()
    {
        return new AppSettings
        {
            Owner = ownerBox.Text.Trim(),
            Repo = repoBox.Text.Trim(),
            Branch = string.IsNullOrWhiteSpace(branchBox.Text) ? "main" : branchBox.Text.Trim(),
            RemotePath = string.IsNullOrWhiteSpace(remotePathBox.Text)
                ? "minecraft-dungeons/default/latest.zip"
                : remotePathBox.Text.Trim().Replace('\\', '/'),
            LocalSaveProfilePath = localPathBox.Text.Trim()
        };
    }

    private void ShowError(string message)
    {
        SetStatus(message);
        MessageBox.Show(this, message, "Settings", MessageBoxButtons.OK, MessageBoxIcon.Error);
    }

    private void SetStatus(string message)
    {
        statusLabel.Text = message;
    }
}
