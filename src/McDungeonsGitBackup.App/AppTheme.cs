namespace McDungeonsGitBackup.App;

internal static class AppTheme
{
    public static readonly Color Background = Color.FromArgb(18, 20, 22);
    public static readonly Color Surface = Color.FromArgb(28, 31, 34);
    public static readonly Color SurfaceAlt = Color.FromArgb(36, 40, 44);
    public static readonly Color Border = Color.FromArgb(74, 79, 73);
    public static readonly Color Text = Color.FromArgb(235, 229, 211);
    public static readonly Color MutedText = Color.FromArgb(164, 158, 143);
    public static readonly Color Gold = Color.FromArgb(214, 167, 72);
    public static readonly Color Emerald = Color.FromArgb(72, 164, 104);
    public static readonly Color Redstone = Color.FromArgb(181, 72, 64);

    public static Font MainFont(float size = 10F, FontStyle style = FontStyle.Regular)
    {
        return new Font("Segoe UI Variable", size, style);
    }

    public static void StyleForm(Form form)
    {
        form.BackColor = Background;
        form.ForeColor = Text;
        form.Font = MainFont();
    }

    public static Button Button(string text, Color accent)
    {
        var button = new Button
        {
            Text = text,
            AutoSize = false,
            Height = 58,
            FlatStyle = FlatStyle.Flat,
            BackColor = SurfaceAlt,
            ForeColor = Text,
            Font = MainFont(11.5F, FontStyle.Bold),
            Margin = new Padding(0),
            Cursor = Cursors.Hand
        };
        button.FlatAppearance.BorderColor = accent;
        button.FlatAppearance.BorderSize = 1;
        button.FlatAppearance.MouseOverBackColor = Color.FromArgb(
            Math.Min(SurfaceAlt.R + 16, 255),
            Math.Min(SurfaceAlt.G + 16, 255),
            Math.Min(SurfaceAlt.B + 16, 255));
        button.FlatAppearance.MouseDownBackColor = accent;
        return button;
    }

    public static Label Label(string text, float size = 10F, Color? color = null, FontStyle style = FontStyle.Regular)
    {
        return new Label
        {
            Text = text,
            AutoSize = true,
            ForeColor = color ?? Text,
            Font = MainFont(size, style)
        };
    }

    public static TextBox TextBox()
    {
        return new TextBox
        {
            BackColor = Color.FromArgb(20, 22, 24),
            ForeColor = Text,
            BorderStyle = BorderStyle.FixedSingle,
            Font = MainFont(10F)
        };
    }
}
