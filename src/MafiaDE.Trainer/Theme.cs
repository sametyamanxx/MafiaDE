using System.Drawing.Drawing2D;

namespace MafiaDE.Trainer;

internal static class Theme
{
    // Charcoal, sunset orange and ivory from the original game's icon.
    public static readonly Color Background = Color.FromArgb(16, 17, 18);
    public static readonly Color Panel = Color.FromArgb(25, 26, 27);
    public static readonly Color Border = Color.FromArgb(73, 55, 47);
    public static readonly Color Accent = Color.FromArgb(218, 111, 53);
    public static readonly Color Bright = Color.FromArgb(244, 164, 107);
    public static readonly Color Muted = Color.FromArgb(172, 159, 147);
    public static readonly Color Text = Color.FromArgb(245, 239, 226);
    public static readonly Color Red = Color.FromArgb(202, 71, 53);
    public static readonly Color Active = Color.FromArgb(248, 188, 133);
    public static readonly Color PanelActive = Color.FromArgb(46, 31, 24);
    public static readonly Color PanelHover = Color.FromArgb(35, 29, 26);
    public static readonly Color Inset = Color.FromArgb(11, 12, 13);
    public static readonly Color Pattern = Color.FromArgb(53, 40, 32);
    public static readonly Color Button = Color.FromArgb(29, 25, 23);
    public static readonly Color ButtonHover = Color.FromArgb(58, 36, 25);
    public static readonly Color DangerButton = Color.FromArgb(32, 21, 20);
    public static readonly Color DangerButtonHover = Color.FromArgb(60, 27, 23);

    public static Font Mono(float size, FontStyle style = FontStyle.Regular) => new("Consolas", size, style);

    public static GraphicsPath Rounded(RectangleF rectangle, float radius)
    {
        var p = new GraphicsPath();
        float d = radius * 2;
        p.AddArc(rectangle.X, rectangle.Y, d, d, 180, 90);
        p.AddArc(rectangle.Right - d, rectangle.Y, d, d, 270, 90);
        p.AddArc(rectangle.Right - d, rectangle.Bottom - d, d, d, 0, 90);
        p.AddArc(rectangle.X, rectangle.Bottom - d, d, d, 90, 90);
        p.CloseFigure();
        return p;
    }

    public static void DrawText(Graphics g, string text, Font font, Color color, Rectangle bounds,
        TextFormatFlags alignment = TextFormatFlags.Left) =>
        TextRenderer.DrawText(g, text, font, bounds, color,
            alignment | TextFormatFlags.NoPadding | TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis);
}

internal sealed class FeatureButton : Button
{
    public string Shortcut { get; init; } = "";
    public string Description { get; set; } = "analiz bekleniyor…";
    public bool On { get; set; }
    private bool hover;
    private readonly Font heading = Theme.Mono(10, FontStyle.Bold);
    private readonly Font detail = Theme.Mono(8);
    private readonly Font tiny = Theme.Mono(7.5f);

    public FeatureButton()
    {
        FlatStyle = FlatStyle.Flat;
        FlatAppearance.BorderSize = 0;
        BackColor = Theme.Panel;
        Cursor = Cursors.Hand;
        SetStyle(ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer |
            ControlStyles.AllPaintingInWmPaint | ControlStyles.SupportsTransparentBackColor, true);
        BackColor = Color.Transparent;
        UseVisualStyleBackColor = false;
    }

    protected override void OnMouseEnter(EventArgs e) { hover = true; Invalidate(); base.OnMouseEnter(e); }
    protected override void OnMouseLeave(EventArgs e) { hover = false; Invalidate(); base.OnMouseLeave(e); }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        float s = DeviceDpi / 96f;
        int S(int n) => (int)Math.Round(n * s);
        using var path = Theme.Rounded(new RectangleF(.5f, .5f, Width - 1, Height - 1), S(8));
        using var fill = new SolidBrush(On ? Theme.PanelActive :
            hover ? Theme.PanelHover : Theme.Panel);
        using var border = new Pen(On || hover || Focused ? Theme.Accent : Theme.Border);
        g.FillPath(fill, path);
        g.DrawPath(border, path);
        var key = new Rectangle(S(14), S(15), S(51), S(22));
        using var keyPath = Theme.Rounded(key, S(4));
        using var keyFill = new SolidBrush(Theme.Inset);
        g.FillPath(keyFill, keyPath);
        g.DrawPath(border, keyPath);
        Theme.DrawText(g, Shortcut, tiny, Theme.Text, key, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
        Theme.DrawText(g, Text, heading, Theme.Text, new(S(80), S(8), Width - S(177), S(19)));
        Theme.DrawText(g, Description, detail, On ? Theme.Bright : Theme.Muted,
            new(S(80), S(30), Width - S(90), S(15)));
        Theme.DrawText(g, On ? "[ AÇIK ]" : "[ KAPALI ]", tiny, On ? Theme.Active : Theme.Muted,
            new(Width - S(91), S(11), S(78), S(19)), TextFormatFlags.Right);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) { heading.Dispose(); detail.Dispose(); tiny.Dispose(); }
        base.Dispose(disposing);
    }
}
