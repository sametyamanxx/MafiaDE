using System.Diagnostics;
using System.Drawing.Drawing2D;

namespace MafiaDE.Trainer;

internal sealed class TrainerWindow : Form
{
    private readonly TrainerEngine engine = new();
    private readonly FeatureButton ammo;
    private readonly FeatureButton health;
    private readonly System.Windows.Forms.Timer keyTimer = new() { Interval = 25 };
    private readonly System.Windows.Forms.Timer connectionTimer = new() { Interval = 1500 };
    private readonly NotifyIcon tray;
    private readonly ContextMenuStrip trayMenu = new();
    private readonly Icon gameIcon;
    private readonly Image gameImage;
    private readonly List<string> logs = [];
    private readonly Font titleFont = Theme.Mono(13, FontStyle.Bold);
    private readonly Font bodyFont = Theme.Mono(10, FontStyle.Bold);
    private readonly Font smallFont = Theme.Mono(8);
    private readonly Font tinyFont = Theme.Mono(7);
    private readonly bool startHidden;
    private readonly bool[] keyDown = new bool[3];
    private string status = "Oyun bekleniyor";
    private string statusDetail = "Mafia DE açılınca otomatik bağlanır.";
    private bool connecting, closeRequested, closing;
    private bool statusError;
    private string? lastError;
    private Rectangle minimizeBounds, closeBounds;
    private Point mouse;
    private int logScroll;

    public TrainerWindow(bool hidden)
    {
        startHidden = hidden;
        Text = "MafiaDE";
        Name = "MafiaDE";
        AutoScaleMode = AutoScaleMode.Dpi;
        AutoScaleDimensions = new SizeF(96, 96);
        ClientSize = new Size(500, 448);
        FormBorderStyle = FormBorderStyle.None;
        StartPosition = FormStartPosition.CenterScreen;
        MaximizeBox = false;
        DoubleBuffered = true;
        BackColor = Theme.Background;
        TopMost = true;
        using (var stream = Program.Asset("mafia.ico")) gameIcon = new Icon(stream);
        using (var stream = Program.Asset("mafia.png"))
        using (var source = Image.FromStream(stream)) gameImage = new Bitmap(source);
        Icon = gameIcon;
        ammo = new FeatureButton { Text = "Sınırsız Mühimmat", Shortcut = "F1",
            AccessibleName = "F1 Sınırsız Mühimmat", TabIndex = 0 };
        health = new FeatureButton { Text = "Sınırsız Sağlık", Shortcut = "F2",
            AccessibleName = "F2 Sınırsız Sağlık", TabIndex = 1 };
        Controls.Add(ammo);
        Controls.Add(health);
        ammo.Click += (_, _) => Toggle(FeatureDefinition.Ammo);
        health.Click += (_, _) => Toggle(FeatureDefinition.Health);
        engine.Log += AddLog;
        trayMenu.Items.Add("Göster / Gizle · Insert", null, (_, _) => ToggleVisibility());
        trayMenu.Items.Add("Çıkış", null, (_, _) => Close());
        tray = new NotifyIcon { Icon = gameIcon, Text = "MafiaDE • Insert ile göster / gizle",
            Visible = true, ContextMenuStrip = trayMenu };
        tray.DoubleClick += (_, _) => ToggleVisibility();
        keyTimer.Tick += (_, _) => CheckKeys();
        connectionTimer.Tick += async (_, _) => await UpdateConnection();
        AddLog("Kısayollar hazır: Insert · F1 · F2");
        AddLog("MafiaDE başladı; oyun bekleniyor…");
    }

    protected override async void OnShown(EventArgs e)
    {
        base.OnShown(e);
        PositionControls();
        keyTimer.Start();
        connectionTimer.Start();
        if (startHidden) Hide();
        await UpdateConnection();
    }

    protected override void OnDpiChanged(DpiChangedEventArgs e)
    {
        base.OnDpiChanged(e);
        PositionControls();
    }

    protected override void OnResize(EventArgs e)
    {
        base.OnResize(e);
        if (ammo != null) PositionControls();
        Invalidate();
    }

    private int S(int n) => (int)Math.Round(n * DeviceDpi / 96f);
    private Rectangle R(int x, int y, int w, int h) => new(S(x), S(y), S(w), S(h));

    private void PositionControls()
    {
        ammo.Bounds = R(22, 188, 456, 52);
        health.Bounds = R(22, 246, 456, 52);
        minimizeBounds = R(410, 5, 34, 24);
        closeBounds = R(452, 5, 34, 24);
    }

    private void ToggleVisibility()
    {
        if (Visible && WindowState != FormWindowState.Minimized) Hide();
        else { WindowState = FormWindowState.Normal; Show(); Activate(); }
    }

    private void CheckKeys()
    {
        int[] keys = [0x2D, 0x70, 0x71];
        for (int i = 0; i < keys.Length; i++)
        {
            bool down = (Native.GetAsyncKeyState(keys[i]) & 0x8000) != 0;
            bool pressed = down && !keyDown[i];
            keyDown[i] = down;
            if (!pressed || closing) continue;
            if (i == 0) { ToggleVisibility(); continue; }
            Native.GetWindowThreadProcessId(Native.GetForegroundWindow(), out int pid);
            if (pid != Environment.ProcessId && pid != engine.ProcessId) continue;
            Toggle(i == 1 ? FeatureDefinition.Ammo : FeatureDefinition.Health);
        }
    }

    private void Toggle(FeatureDefinition feature)
    {
        if (connecting) { AddLog("Analiz sürüyor; lütfen bekle."); return; }
        try { engine.Toggle(feature); }
        catch (Exception ex) { AddLog(ex.Message); }
        RefreshFeatures();
    }

    private async Task UpdateConnection()
    {
        if (connecting || closing) return;
        if (engine.Connected) return;
        try
        {
            if (engine.ProcessId != null)
            {
                engine.Disconnect();
                AddLog("Oyun kapandı; özellikler sıfırlandı.");
            }
            Process[] processes = Process.GetProcessesByName("mafiadefinitiveedition");
            if (processes.Length == 0)
            {
                status = "Oyun bekleniyor";
                statusDetail = "Mafia DE açılınca otomatik bağlanır.";
                statusError = false;
                lastError = null;
                return;
            }
            Process process = processes[0];
            foreach (var extra in processes.Skip(1)) extra.Dispose();
            connecting = true;
            status = "Oyun analiz ediliyor";
            statusDetail = "Dosya sürümü ve bellek imzaları denetleniyor.";
            statusError = false;
            Invalidate();
            try { await Task.Run(() => engine.Connect(process)); }
            catch { process.Dispose(); throw; }
            status = "Oyun bağlandı";
            statusDetail = "İmzalar doğrulandı • F1 / F2 hazır.";
            lastError = null;
        }
        catch (Exception ex)
        {
            status = "Bağlantı kurulamadı";
            statusDetail = ex.Message;
            statusError = true;
            if (lastError != ex.Message) AddLog(ex.Message);
            lastError = ex.Message;
        }
        finally
        {
            connecting = false;
            RefreshFeatures();
            Invalidate();
            if (closeRequested) Close();
        }
    }

    private void RefreshFeatures()
    {
        ammo.On = engine.Enabled(FeatureDefinition.Ammo);
        health.On = engine.Enabled(FeatureDefinition.Health);
        ammo.Description = ammo.On ? "bellek yaması etkin • yedek mermi: 99" :
            engine.Connected ? "imza doğrulandı • açmak için F1" : "analiz bekleniyor…";
        health.Description = health.On ? "bellek yaması etkin • maksimum sağlık" :
            engine.Connected ? "imza doğrulandı • açmak için F2" : "analiz bekleniyor…";
        ammo.Invalidate();
        health.Invalidate();
    }

    private void AddLog(string text)
    {
        if (InvokeRequired) { BeginInvoke(() => AddLog(text)); return; }
        // Wrap the output into short lines; keep the full error in the status / dialog.
        string line = $"[{DateTime.Now:HH:mm:ss}] {text.Replace("\r", "").Replace("\n", " ")}";
        logs.Add(line);
        if (logs.Count > 200) logs.RemoveAt(0);
        logScroll = 0;
        Invalidate();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        Graphics g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        using var border = new Pen(Theme.Border);
        using var accent = new Pen(Theme.Accent);
        using var dot = new SolidBrush(Theme.Pattern);
        for (int y = S(48); y < Height - S(12); y += S(24))
            for (int x = S(22); x < Width - S(12); x += S(24)) g.FillRectangle(dot, x, y, 1, 1);
        g.DrawRectangle(border, 0, 0, Width - 1, Height - 1);
        g.DrawLine(border, 0, S(34), Width, S(34));
        foreach (var corner in new[] { (0, 0, 1, 1), (499, 0, -1, 1), (0, 447, 1, -1), (499, 447, -1, -1) })
        {
            int x = S(corner.Item1), y = S(corner.Item2);
            g.DrawLine(accent, x, y, x + S(12) * corner.Item3, y);
            g.DrawLine(accent, x, y, x, y + S(12) * corner.Item4);
        }
        PaintWindowButton(g, minimizeBounds, false);
        PaintWindowButton(g, closeBounds, true);
        g.DrawImage(gameImage, R(22, 43, 38, 38));
        Theme.DrawText(g, "Mafia: Definitive Edition", titleFont, Theme.Text, R(76, 50, 408, 25));

        using var panel = new SolidBrush(Theme.Panel);
        g.FillRectangle(panel, R(22, 101, 456, 64));
        g.DrawRectangle(border, R(22, 101, 455, 63));
        using var ring = new Pen(statusError ? Theme.Red : Theme.Accent, S(2));
        g.DrawEllipse(ring, R(41, 129, 14, 14));
        using var indicator = new SolidBrush(statusError ? Theme.Red : engine.Connected ? Theme.Active : Theme.Accent);
        g.FillEllipse(indicator, R(46, 134, 4, 4));
        Theme.DrawText(g, "[SYS]", tinyFont, Theme.Muted, R(71, 107, 80, 13));
        Theme.DrawText(g, status, bodyFont, statusError ? Theme.Red : Theme.Bright, R(71, 123, 395, 19));
        Theme.DrawText(g, statusDetail, smallFont, Theme.Muted, R(71, 142, 395, 17));
        Theme.DrawText(g, "KOMUTLAR", tinyFont, Theme.Accent, R(25, 174, 64, 13));
        g.DrawLine(border, S(81), S(180), S(477), S(180));

        using var logPath = Theme.Rounded(R(22, 309, 456, 107), S(8));
        using var black = new SolidBrush(Theme.Inset);
        g.FillPath(black, logPath);
        g.DrawPath(border, logPath);
        Theme.DrawText(g, "› ÇIKTI", tinyFont, Theme.Accent, R(35, 315, 200, 14));
        g.DrawLine(border, S(35), S(331), S(466), S(331));
        int last = logs.Count - logScroll;
        int first = Math.Max(0, last - 5);
        for (int i = first; i < last; i++)
            Theme.DrawText(g, logs[i], smallFont, Theme.Muted, R(36, 336 + (i - first) * 14, 428, 16));
        Theme.DrawText(g, "INSERT · GÖSTER / GİZLE", tinyFont, Theme.Muted, R(24, 428, 255, 13));
        Theme.DrawText(g, "v1.0", tinyFont, Theme.Muted, R(439, 428, 36, 13), TextFormatFlags.Right);
    }

    private void PaintWindowButton(Graphics g, Rectangle bounds, bool close)
    {
        bool hovered = bounds.Contains(mouse);
        Color color = close ? Theme.Red : Theme.Accent;
        using var p = Theme.Rounded(bounds, S(5));
        using var fill = new SolidBrush(close
            ? hovered ? Theme.DangerButtonHover : Theme.DangerButton
            : hovered ? Theme.ButtonHover : Theme.Button);
        using var pen = new Pen(color);
        g.FillPath(fill, p);
        g.DrawPath(pen, p);
        int x = bounds.X + bounds.Width / 2, y = bounds.Y + bounds.Height / 2;
        if (close)
        {
            g.DrawLine(pen, x - S(4), y - S(4), x + S(4), y + S(4));
            g.DrawLine(pen, x + S(4), y - S(4), x - S(4), y + S(4));
        }
        else g.DrawLine(pen, x - S(5), y, x + S(5), y);
    }

    protected override void OnMouseMove(MouseEventArgs e) { mouse = e.Location; Invalidate(); base.OnMouseMove(e); }
    protected override void OnMouseLeave(EventArgs e) { mouse = new(-1, -1); Invalidate(); base.OnMouseLeave(e); }
    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        if (e.Button != MouseButtons.Left) return;
        if (closeBounds.Contains(e.Location)) { Close(); return; }
        if (minimizeBounds.Contains(e.Location)) { Hide(); return; }
        if (e.Y < S(96))
        {
            Native.ReleaseCapture();
            Native.SendMessage(Handle, 0xA1, 2, 0); // drag borderless caption
        }
    }

    protected override void OnMouseWheel(MouseEventArgs e)
    {
        if (R(22, 309, 456, 107).Contains(e.Location))
        {
            logScroll = Math.Clamp(logScroll + (e.Delta > 0 ? 1 : -1), 0, Math.Max(0, logs.Count - 5));
            Invalidate();
        }
        base.OnMouseWheel(e);
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        if (connecting) { closeRequested = true; e.Cancel = true; return; }
        try { engine.Disconnect(); }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "MafiaDE", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            e.Cancel = true;
            return;
        }
        closing = true;
        keyTimer.Stop();
        connectionTimer.Stop();
        tray.Visible = false;
        base.OnFormClosing(e);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            keyTimer.Dispose(); connectionTimer.Dispose(); tray.Dispose(); trayMenu.Dispose();
            gameImage.Dispose(); gameIcon.Dispose();
            titleFont.Dispose(); bodyFont.Dispose(); smallFont.Dispose(); tinyFont.Dispose();
        }
        base.Dispose(disposing);
    }
}
