using McDungeonsGitBackup.Core;

namespace McDungeonsGitBackup.App;

public sealed class ProfilePickerForm : Form
{
    private readonly ListBox listBox = new();

    public ProfilePickerForm(IReadOnlyList<SaveProfile> profiles)
    {
        Text = "Select save profile";
        Width = 760;
        Height = 360;
        StartPosition = FormStartPosition.CenterParent;
        AppTheme.StyleForm(this);

        listBox.Dock = DockStyle.Fill;
        listBox.DataSource = profiles.ToList();
        listBox.DoubleClick += (_, _) => AcceptSelection();
        listBox.BackColor = AppTheme.Surface;
        listBox.ForeColor = AppTheme.Text;
        listBox.BorderStyle = BorderStyle.FixedSingle;
        listBox.Font = AppTheme.MainFont(10F);

        var okButton = AppTheme.Button("Select", AppTheme.Gold);
        okButton.Width = 110;
        okButton.Click += (_, _) => AcceptSelection();

        var cancelButton = AppTheme.Button("Cancel", AppTheme.Border);
        cancelButton.Width = 110;
        cancelButton.Click += (_, _) => DialogResult = DialogResult.Cancel;

        var buttons = new FlowLayoutPanel
        {
            Dock = DockStyle.Bottom,
            FlowDirection = FlowDirection.RightToLeft,
            Height = 46,
            Padding = new Padding(8),
            BackColor = AppTheme.Background
        };
        buttons.Controls.Add(cancelButton);
        buttons.Controls.Add(okButton);

        Controls.Add(listBox);
        Controls.Add(buttons);
    }

    public SaveProfile? SelectedProfile { get; private set; }

    private void AcceptSelection()
    {
        SelectedProfile = listBox.SelectedItem as SaveProfile;
        if (SelectedProfile is not null)
        {
            DialogResult = DialogResult.OK;
        }
    }
}
