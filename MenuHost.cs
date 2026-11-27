// The menu window: hotkey, layout, text pad, opening and closing, keys, mouse and drawing.
// Running items is in MenuHost.Launch.cs, updates in MenuHost.Update.cs.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Management;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Web.Script.Serialization;
using System.Windows.Forms;

sealed partial class MenuHost : Form
{
    string latestVersion;                       // newer version on GitHub, or null
    int footerHover = -1;
    readonly System.Windows.Forms.Timer updateTimer = new System.Windows.Forms.Timer();
    bool checkingUpdates;
    DateTime lastUpdateCheck = DateTime.MinValue;

    const int WM_HOTKEY = 0x0312;
    const int HOTKEY_ID = 1;
    const uint MOD_ALT = 0x0001;
    const uint MOD_WIN = 0x0008;
    const int VK_X = 0x58;
    const byte VK_NOP = 0xE8;
    int listWidth = 200;       // set in LayoutMenu to fit the longest name
    int ListWidth { get { return listWidth; } }
    const int ListGap = 50;        // space between the list and the text pad
    const int MaxRows = 18;      // rows shown at once; the rest scroll
    const int PadWidth = 440;
    const int HeaderHeight = 34;   // top of the list, below the hint
    const int PadTop = 34;         // top of the text pad (its title sits above it)
    const int RowHeight = 22;
    const int MinHeight = 340;
    const string AppVersion = "1.0.31";   // also in VERSION
    public static string Version { get { return AppVersion; } }
    const string RepoOwner = "donkeyhigh";
    const string RepoName = "WindowsMenuTool";
    const string RawRoot = "https://raw.githubusercontent.com/" + RepoOwner + "/" + RepoName + "/";
    const string RawBase = RawRoot + "main/";
    const string ApiCommit = "https://api.github.com/repos/" + RepoOwner + "/" + RepoName + "/commits/main";
    uint hotkeyMods, hotkeyKey;   // the menu hotkey as it is registered now
    bool hotkeyOn;

    [DllImport("user32.dll")] static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);
    [DllImport("user32.dll")] static extern bool UnregisterHotKey(IntPtr hWnd, int id);
    [DllImport("user32.dll")] static extern void keybd_event(byte bVk, byte bScan, uint dwFlags, UIntPtr dwExtraInfo);
    [DllImport("user32.dll")] static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] static extern uint GetWindowThreadProcessId(IntPtr hWnd, IntPtr lpdwProcessId);
    [DllImport("user32.dll")] static extern bool AttachThreadInput(uint idAttach, uint idAttachTo, bool fAttach);
    [DllImport("user32.dll")] static extern bool SetForegroundWindow(IntPtr hWnd);
    [DllImport("user32.dll")] static extern bool AllowSetForegroundWindow(int dwProcessId);
    [DllImport("kernel32.dll")] static extern uint GetCurrentThreadId();
    [DllImport("user32.dll")] static extern IntPtr WindowFromPoint(System.Drawing.Point p);
    [DllImport("user32.dll")] static extern IntPtr GetAncestor(IntPtr hwnd, uint flags);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern int GetClassName(IntPtr hwnd, StringBuilder name, int max);
    delegate bool EnumWindowsProc(IntPtr hwnd, IntPtr lParam);
    [DllImport("user32.dll")] static extern bool EnumWindows(EnumWindowsProc proc, IntPtr lParam);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern IntPtr FindWindowEx(IntPtr parent, IntPtr after, string cls, string title);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern int GetWindowText(IntPtr hwnd, StringBuilder text, int max);
    [DllImport("user32.dll", EntryPoint = "GetWindowThreadProcessId")] static extern uint GetWindowPid(IntPtr hwnd, out uint pid);
    [DllImport("user32.dll")] static extern bool IsIconic(IntPtr hwnd);
    [DllImport("user32.dll")] static extern bool ShowWindow(IntPtr hwnd, int cmd);

    static readonly Point Parked = new Point(-32000, -32000);

    List<Entry> entries;                       // everything, including hidden entries, in order
    readonly List<Entry> shown = new List<Entry>();
    string folderUnderMouse;                   // folder under the mouse when the menu opened
    readonly System.Windows.Forms.Timer timer = new System.Windows.Forms.Timer();
    readonly NotifyIcon tray = new NotifyIcon();
    readonly Font font = new Font("Segoe UI", 9f);
    readonly Font headFont = new Font("Segoe UI", 8.5f);
    readonly Label title = new Label();
    readonly TextBox pad = new TextBox();
    readonly Label padStatus = new Label();
    SettingsForm settings;
    bool open;
    int hover = -1;
    int top;                   // index of the first item shown (the list scrolls)
    DateTime lastKey;
    IntPtr previousForeground = IntPtr.Zero;

    public MenuHost()
    {
        Text = "WinTools Menu";
        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        TopMost = true;
        StartPosition = FormStartPosition.Manual;
        BackColor = Color.FromArgb(17, 24, 39);
        ForeColor = Color.White;
        DoubleBuffered = true;
        KeyPreview = true;

        entries = Config.Load();
        Rebuild();
        BuildPad();
        LayoutMenu();
        updateTimer.Interval = 24 * 60 * 60 * 1000;
        updateTimer.Tick += delegate { CheckForUpdates(); };
        updateTimer.Start();

        timer.Interval = 1000;
        timer.Tick += delegate { OnTimer(); };

        tray.Icon = Dialogs.AppIcon(SystemInformation.SmallIconSize);
        tray.Text = "WinTools Menu " + AppVersion + " (" + Config.Hotkey + ")";
        tray.Visible = true;
        ContextMenuStrip cm = new ContextMenuStrip();
        cm.Items.Add("Custom shortcuts (" + Config.Hotkey + " twice)", null, delegate { ShowSettings(); });
        cm.Items.Add("Check for updates", null, delegate { CheckNowIgnoringSkip(); });
        cm.Items.Add("Exit WinTools Menu", null, delegate { Application.Exit(); });
        tray.ContextMenuStrip = cm;

        // Parked off-screen while closed. Hiding the window made Windows stop delivering the hotkey.
        Location = Parked;
        if (!Config.TryParseHotkey(Config.Hotkey, out hotkeyMods, out hotkeyKey))
        {
            Config.Hotkey = "Win+Alt+X";
            Config.TryParseHotkey(Config.Hotkey, out hotkeyMods, out hotkeyKey);
        }
        if (!RegisterOwnHotkey(hotkeyMods, hotkeyKey))
        {
            tray.Text = "WinTools Menu: " + Config.Hotkey + " is used by another program";
        }
        CheckForUpdates();
    }

    bool UnregisterOwnHotkey()
    {
        if (hotkeyOn) UnregisterHotKey(Handle, HOTKEY_ID);
        hotkeyOn = false;
        return true;
    }

    bool RegisterOwnHotkey(uint mods, uint key)
    {
        UnregisterOwnHotkey();
        hotkeyOn = RegisterHotKey(Handle, HOTKEY_ID, mods, key);
        return hotkeyOn;
    }

    // While the settings window records a new combination, the old hotkey must not fire.
    public void SuspendHotkey() { UnregisterOwnHotkey(); }
    public void ResumeHotkey() { if (!hotkeyOn) RegisterOwnHotkey(hotkeyMods, hotkeyKey); }

    // Registers a new hotkey text such as "Ctrl+Alt+M". Returns false and keeps the old one if Windows refuses it.
    public bool ChangeHotkey(string text)
    {
        uint mods, key;
        if (!Config.TryParseHotkey(text, out mods, out key)) return false;
        if (RegisterOwnHotkey(mods, key))
        {
            hotkeyMods = mods;
            hotkeyKey = key;
            tray.Text = "WinTools Menu " + AppVersion + " (" + text + ")";
            return true;
        }
        RegisterOwnHotkey(hotkeyMods, hotkeyKey);
        return false;
    }

    // Called by the settings window after Save.
    public void Reload(List<Entry> newEntries)
    {
        entries = newEntries;
        Rebuild();
        hover = -1;
        LayoutMenu();
        Invalidate();
    }

    void Rebuild()
    {
        shown.Clear();
        foreach (Entry e in entries)
        {
            if (!e.Hidden) shown.Add(e);
        }
    }

    int VisibleRows() { return Math.Max(1, Math.Min(shown.Count, MaxRows)); }
    int MaxTop() { return Math.Max(0, shown.Count - VisibleRows()); }

    // The list is only as wide as its longest name (and footer item), so the gap to the text pad is measured from the text.
    int ComputeListWidth()
    {
        int widest = 0;
        foreach (Entry e in shown) widest = Math.Max(widest, TextRenderer.MeasureText(e.Title ?? "", font).Width);
        foreach (FooterItem f in Footer()) widest = Math.Max(widest, TextRenderer.MeasureText(f.Label, font).Width);
        widest = Math.Max(widest, TextRenderer.MeasureText(Config.Hotkey + " again: customise", headFont).Width);
        return Math.Max(120, widest + 16);
    }

    void LayoutMenu()
    {
        top = Math.Min(top, MaxTop());
        listWidth = ComputeListWidth();
        int listW = ListWidth;
        int height = Math.Max(FooterTop(VisibleRows()) + Footer().Count * RowHeight + 8, MinHeight);
        ClientSize = new Size(listW + ListGap + PadWidth + 8, height);
        int px = listW + ListGap;
        title.Location = new Point(px, 8);
        pad.Location = new Point(px, PadTop);
        pad.Size = new Size(PadWidth, height - PadTop - 34);
        padStatus.Location = new Point(px, height - 26);
    }

    void BuildPad()
    {
        title.Text = "Plain text: paste here, then copy with Ctrl+C";
        title.Font = headFont;
        title.ForeColor = Color.FromArgb(156, 163, 175);
        title.AutoSize = true;
        Controls.Add(title);

        pad.Multiline = true;
        pad.WordWrap = true;
        pad.ScrollBars = ScrollBars.Vertical;
        pad.AcceptsReturn = true;
        pad.BorderStyle = BorderStyle.FixedSingle;
        pad.BackColor = Color.FromArgb(31, 41, 55);
        pad.ForeColor = Color.White;
        pad.Font = new Font("Segoe UI", 10f);
        pad.TextChanged += delegate
        {
            lastKey = DateTime.Now;
            padStatus.Text = pad.TextLength + " characters";
        };
        Controls.Add(pad);

        padStatus.Text = "0 characters";
        padStatus.ForeColor = Color.FromArgb(156, 163, 175);
        padStatus.Font = headFont;
        padStatus.AutoSize = true;
        Controls.Add(padStatus);

    }

    protected override void WndProc(ref Message m)
    {
        if (m.Msg == WM_HOTKEY && m.WParam.ToInt32() == HOTKEY_ID)
        {
            // Tap an unused key so Windows does not open Start when Win is released.
            keybd_event(VK_NOP, 0, 0, UIntPtr.Zero);
            keybd_event(VK_NOP, 0, 2, UIntPtr.Zero);
            // Win+Alt+X opens the menu. Pressed again while the menu is open, it opens Custom Shortcuts.
            if (open) ShowSettings();
            else OpenMenu();
            return;
        }
        base.WndProc(ref m);
    }


    void OpenMenu()
    {
        if (open) return;
        open = true;
        lastKey = DateTime.Now;
        folderUnderMouse = FolderUnderCursor();
        Point p = Cursor.Position;
        Rectangle wa = Screen.FromPoint(p).WorkingArea;
        int x = Math.Max(wa.Left, Math.Min(p.X, wa.Right - Width));
        int y = Math.Max(wa.Top, Math.Min(p.Y, wa.Bottom - Height));
        Location = new Point(x, y);
        timer.Start();
        TakeFocus();
        Invalidate();
        // Look for a new version each time the menu opens (at most once a minute).
        if (DateTime.Now - lastUpdateCheck > TimeSpan.FromMinutes(1)) CheckForUpdates();
    }

    // Windows only lets a window take focus if it is allowed to. Attaching to the current foreground
    // thread for a moment makes SetForegroundWindow work, so the letter keys reach this window.
    void TakeFocus()
    {
        IntPtr fg = GetForegroundWindow();
        if (fg != Handle) previousForeground = fg;
        uint fgThread = GetWindowThreadProcessId(fg, IntPtr.Zero);
        uint me = GetCurrentThreadId();
        bool attached = fgThread != 0 && fgThread != me && AttachThreadInput(me, fgThread, true);
        SetForegroundWindow(Handle);
        if (attached) AttachThreadInput(me, fgThread, false);
        Activate();
        // Opening must not leave the text pad focused, or the letters would be typed into it.
        ActiveControl = null;
        Focus();
    }

    // giveBackFocus false: the menu keeps the foreground so a program started right after can take it over.
    void CloseMenu(bool giveBackFocus = true)
    {
        if (!open) return;
        open = false;
        hover = -1;
        top = 0;
        timer.Stop();
        Location = Parked;
        // The parked window would otherwise keep the keyboard. Give it back to the window that had it.
        if (giveBackFocus) GiveFocusTo(previousForeground);
        previousForeground = IntPtr.Zero;
    }

    void GiveFocusTo(IntPtr hwnd)
    {
        if (hwnd == IntPtr.Zero || hwnd == Handle) return;
        uint fgThread = GetWindowThreadProcessId(hwnd, IntPtr.Zero);
        uint me = GetCurrentThreadId();
        bool attached = fgThread != 0 && fgThread != me && AttachThreadInput(me, fgThread, true);
        SetForegroundWindow(hwnd);
        if (attached) AttachThreadInput(me, fgThread, false);
    }

    void ShowSettings()
    {
        CloseMenu();
        if (settings == null || settings.IsDisposed)
        {
            List<Entry> copy = new List<Entry>();
            foreach (Entry e in entries) copy.Add(e.Clone());
            settings = new SettingsForm(this, copy);
        }
        settings.Show();
        settings.Activate();
    }

    // Clicking outside (or switching to another window) takes focus away and closes the menu.
    protected override void OnDeactivate(EventArgs e)
    {
        base.OnDeactivate(e);
        CloseMenu();
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (!open) return;
        lastKey = DateTime.Now;
        if (e.KeyCode == Keys.Escape) { e.SuppressKeyPress = true; CloseMenu(); return; }
        // Typing in the pad: letters are text, not shortcuts.
        if (pad.Focused) return;
        if (e.KeyCode == Keys.Down) { e.SuppressKeyPress = true; ScrollBy(1); return; }
        if (e.KeyCode == Keys.Up) { e.SuppressKeyPress = true; ScrollBy(-1); return; }
        if (e.KeyCode == Keys.PageDown) { e.SuppressKeyPress = true; ScrollBy(VisibleRows()); return; }
        if (e.KeyCode == Keys.PageUp) { e.SuppressKeyPress = true; ScrollBy(-VisibleRows()); return; }
        if (e.KeyCode >= Keys.A && e.KeyCode <= Keys.Z)
        {
            char c = (char)('A' + (int)(e.KeyCode - Keys.A));
            foreach (Entry en in shown)
            {
                if (en.Letter == c.ToString() && ItemEnabled(en))
                {
                    e.SuppressKeyPress = true;
                    Launch(en);
                    return;
                }
            }
        }
    }

    void OnTimer()
    {
        if ((DateTime.Now - lastKey).TotalSeconds > 60) CloseMenu();
    }

    // The item on the row under (x, y), counting from the scroll position. -1 if there is none.
    int ItemAt(int x, int y)
    {
        if (x < 0 || x >= ListWidth || y < HeaderHeight) return -1;
        int row = (y - HeaderHeight) / RowHeight;
        if (row >= VisibleRows()) return -1;
        int index = top + row;
        return index < shown.Count ? index : -1;
    }

    // Moves the list by whole rows. Used by the mouse wheel and the arrow, Page Up and Page Down keys.
    void ScrollBy(int rows)
    {
        int next = Math.Max(0, Math.Min(MaxTop(), top + rows));
        if (next == top) return;
        top = next;
        hover = -1;
        Invalidate();
    }

    protected override void OnMouseWheel(MouseEventArgs e)
    {
        base.OnMouseWheel(e);
        ScrollBy(e.Delta > 0 ? -3 : 3);
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        int h = ItemAt(e.X, e.Y);
        int fh = FooterAt(e.X, e.Y);
        if (h != hover || fh != footerHover)
        {
            hover = h;
            footerHover = fh;
            Invalidate();
        }
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        if (!open || e.Button != MouseButtons.Left) return;
        int fi = FooterAt(e.X, e.Y);
        if (fi >= 0)
        {
            FooterItem footerItem = Footer()[fi];
            CloseMenu();
            BeginInvoke(new Action(footerItem.Run));
            return;
        }
        int i = ItemAt(e.X, e.Y);
        if (i < 0 || !ItemEnabled(shown[i])) return;
        Launch(shown[i]);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        Graphics g = e.Graphics;
        // Hint above the list, in the same style as the "Plain text" title above the pad.
        TextRenderer.DrawText(g, Config.Hotkey + " again: customise", headFont,
            new Rectangle(10, 8, ListWidth - 10, 14), Color.FromArgb(156, 163, 175), TextFormatFlags.NoPadding);
        int visible = VisibleRows();
        for (int row = 0; row < visible && top + row < shown.Count; row++)
        {
            int i = top + row;
            Rectangle r = new Rectangle(0, HeaderHeight + row * RowHeight, ListWidth, RowHeight);
            if (i == hover)
            {
                using (SolidBrush b = new SolidBrush(Color.FromArgb(55, 65, 81)))
                {
                    g.FillRectangle(b, r);
                }
            }
            DrawItem(g, shown[i], r);
        }
        DrawFooter(g);
    }

    int FooterTop(int rows) { return HeaderHeight + rows * RowHeight + 10; }

    List<FooterItem> Footer()
    {
        List<FooterItem> f = new List<FooterItem>();
        bool updateWaiting = latestVersion != null && latestVersion != Config.Skipped;
        if (updateWaiting)
        {
            f.Add(new FooterItem("Update to " + latestVersion, delegate { StartUpdate(); }));
            f.Add(new FooterItem("Skip this version", delegate { SkipCurrentUpdate(); }));
        }
        return f;
    }

    int FooterAt(int x, int y)
    {
        if (x < 0 || x >= ListWidth) return -1;
        int footTop = FooterTop(VisibleRows());
        if (y < footTop) return -1;
        int i = (y - footTop) / RowHeight;
        return i < Footer().Count ? i : -1;
    }

    void DrawFooter(Graphics g)
    {
        if (Footer().Count == 0) return;
        int footTop = FooterTop(VisibleRows());
        using (Pen sep = new Pen(Color.FromArgb(55, 65, 81)))
        {
            g.DrawLine(sep, 10, footTop - 5, ListWidth - 10, footTop - 5);
        }
        List<FooterItem> f = Footer();
        for (int i = 0; i < f.Count; i++)
        {
            Rectangle r = new Rectangle(0, footTop + i * RowHeight, ListWidth, RowHeight);
            if (i == footerHover)
            {
                using (SolidBrush b = new SolidBrush(Color.FromArgb(55, 65, 81))) { g.FillRectangle(b, r); }
            }
            TextRenderer.DrawText(g, f[i].Label, font, new Rectangle(r.X + 8, r.Y, r.Width - 8, r.Height),
                Color.FromArgb(209, 213, 219), TextFormatFlags.NoPadding | TextFormatFlags.SingleLine | TextFormatFlags.VerticalCenter);
        }
    }

    // ----- updates (read from the public repo over HTTPS; nothing to install or log in to) -----


    void DrawItem(Graphics g, Entry en, Rectangle r)
    {
        TextFormatFlags flags = TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine | TextFormatFlags.VerticalCenter;
        Rectangle textRect = new Rectangle(r.X + 8, r.Y, r.Width - 8, r.Height);
        Color textColor = ItemEnabled(en) ? Color.White : Color.FromArgb(107, 114, 128);
        string label = en.Title ?? "";
        TextRenderer.DrawText(g, label, font, textRect, textColor, flags);

        // Underline the letter in the name. If the name no longer has it, show it after the name.
        char key = string.IsNullOrEmpty(en.Letter) ? '\0' : en.Letter[0];
        if (key == '\0') return;
        int idx = -1;
        for (int i = 0; i < label.Length; i++)
        {
            if (char.ToUpperInvariant(label[i]) == key) { idx = i; break; }
        }
        Size big = new Size(int.MaxValue, int.MaxValue);
        if (idx < 0)
        {
            string tag = "(" + en.Letter + ")";
            int tx = textRect.X + TextRenderer.MeasureText(g, label, font, big, flags).Width + 6;
            TextRenderer.DrawText(g, tag, font, new Rectangle(tx, r.Y, r.Width, r.Height), Color.FromArgb(251, 191, 36), flags);
            return;
        }
        string pre = label.Substring(0, idx);
        string letter = label.Substring(idx, 1);
        int x0 = textRect.X + (pre.Length == 0 ? 0 : TextRenderer.MeasureText(g, pre, font, big, flags).Width);
        int w = TextRenderer.MeasureText(g, letter, font, big, flags).Width;
        int underlineY = r.Y + r.Height - 6;
        using (Pen pen = new Pen(Color.FromArgb(251, 191, 36), 2f))
        {
            g.DrawLine(pen, x0, underlineY, x0 + w, underlineY);
        }
    }

    protected override void OnFormClosed(FormClosedEventArgs e)
    {
        UnregisterOwnHotkey();
        tray.Visible = false;
        tray.Dispose();
        base.OnFormClosed(e);
    }
}
