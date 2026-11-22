// WinTools menu (Win+Alt+X). Items, letters, hidden entries and order come from %LOCALAPPDATA%\WinTools\menu.json.
// Win+Alt+X again while the menu is open opens Custom Shortcuts: untick to hide, rename, move, add files.
// Two columns; a plain text pad on the right. RegisterHotKey (no keyboard hook).
// Esc, a click outside (focus leaves the window), or 60 seconds without a key closes the menu.
// C# 5 (csc.exe from .NET Framework 4). References: System.Windows.Forms, System.Drawing, System.Web.Extensions.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Web.Script.Serialization;
using System.Windows.Forms;

static class Program
{
    [STAThread]
    static void Main()
    {
        bool created;
        using (Mutex m = new Mutex(true, "WinToolsMenuSingleInstance", out created))
        {
            if (!created) return;
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new MenuHost());
        }
    }
}

// One line in the menu. Built-in entries and custom entries look the same in the file.
public sealed class Entry
{
    public string Id { get; set; }
    public bool Custom { get; set; }
    public string Title { get; set; }
    public string Target { get; set; }
    public string Args { get; set; }
    public string Letter { get; set; }
    public bool Hidden { get; set; }
    public bool NeedsFolder { get; set; }

    public Entry Clone()
    {
        return new Entry { Id = Id, Custom = Custom, Title = Title, Target = Target, Args = Args, Letter = Letter, Hidden = Hidden, NeedsFolder = NeedsFolder };
    }
}

public sealed class MenuFile
{
    public List<Entry> Entries { get; set; }
}

static class Config
{
    public static readonly string Dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "WinTools");
    public static readonly string FilePath = Path.Combine(Dir, "menu.json");

    // Reads menu.json. A broken file is kept as menu.json.bad and the built-in list is used.
    // Built-in entries that the file does not know yet are added at the end.
    public static List<Entry> Load()
    {
        List<Entry> saved = null;
        if (File.Exists(FilePath))
        {
            try
            {
                MenuFile f = new JavaScriptSerializer().Deserialize<MenuFile>(File.ReadAllText(FilePath, Encoding.UTF8));
                if (f != null && f.Entries != null) saved = f.Entries;
            }
            catch (Exception)
            {
                try { File.Move(FilePath, FilePath + ".bad"); } catch (Exception) { }
            }
        }
        if (saved == null) return Defaults();
        List<Entry> result = new List<Entry>(saved);
        foreach (Entry d in Defaults())
        {
            bool known = false;
            foreach (Entry s in result)
            {
                if (s.Id == d.Id) { known = true; break; }
            }
            if (!known) result.Add(d);
        }
        return result;
    }

    public static void Save(List<Entry> entries)
    {
        Directory.CreateDirectory(Dir);
        string json = new JavaScriptSerializer().Serialize(new MenuFile { Entries = entries });
        File.WriteAllText(FilePath, json, Encoding.UTF8);
    }

    static Entry D(string id, string title, string target, string args, string letter, bool folder)
    {
        return new Entry { Id = id, Title = title, Target = target, Args = args, Letter = letter, NeedsFolder = folder };
    }

    public static List<Entry> Defaults()
    {
        List<Entry> l = new List<Entry>();
        l.Add(D("firewall", "firewall.cpl", "{system}\\control.exe", "firewall.cpl", "F", false));
        l.Add(D("appwiz", "Add remove programs", "{system}\\control.exe", "appwiz.cpl", "R", false));
        l.Add(D("ncpa", "Advanced network settings", "{system}\\control.exe", "ncpa.cpl", "W", false));
        l.Add(D("taskmgr", "Task Manager", "{system}\\taskmgr.exe", "", "T", false));
        l.Add(D("devmgmt", "Device Manager", "{system}\\devmgmt.msc", "", "G", false));
        l.Add(D("compmgmt", "Computer Management", "{system}\\compmgmt.msc", "", "M", false));
        l.Add(D("mmsys", "Sounds", "{system}\\control.exe", "mmsys.cpl", "S", false));
        l.Add(D("notepad", "notepad", "{system}\\notepad.exe", "", "E", false));
        l.Add(D("npp", "notepad++", "{programfiles}\\Notepad++\\notepad++.exe", "", "A", false));
        l.Add(D("appdata", "%appdata%", "{system}\\explorer.exe", "\"{appdata}\"", "D", false));
        l.Add(D("localappdata", "%localappdata%", "{system}\\explorer.exe", "\"{localappdata}\"", "C", false));
        l.Add(D("startup", "Shell: startup", "{system}\\explorer.exe", "shell:startup", "U", false));
        l.Add(D("cmd", "Open CMD here", "{system}\\cmd.exe", "", "O", true));
        l.Add(D("pwsh", "Open PowerShell here", "{system}\\WindowsPowerShell\\v1.0\\powershell.exe", "", "P", true));
        return l;
    }

    public static string Expand(string s)
    {
        if (string.IsNullOrEmpty(s)) return s;
        return s.Replace("{system}", Environment.GetFolderPath(Environment.SpecialFolder.System))
                .Replace("{appdata}", Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData))
                .Replace("{localappdata}", Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData))
                .Replace("{programfiles}", Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles))
                .Replace("{programfiles86}", Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86));
    }
}

static class Dialogs
{
    // A small text prompt. Returns the new text, or null if cancelled.
    public static string Prompt(string caption, string value)
    {
        using (Form f = new Form())
        {
            f.Text = caption;
            f.FormBorderStyle = FormBorderStyle.FixedDialog;
            f.StartPosition = FormStartPosition.CenterScreen;
            f.MaximizeBox = false;
            f.MinimizeBox = false;
            f.ClientSize = new Size(360, 100);
            TextBox tb = new TextBox();
            tb.Text = value;
            tb.Location = new Point(12, 14);
            tb.Width = 336;
            Button ok = new Button();
            ok.Text = "OK";
            ok.DialogResult = DialogResult.OK;
            ok.Location = new Point(176, 56);
            ok.Width = 80;
            Button cancel = new Button();
            cancel.Text = "Cancel";
            cancel.DialogResult = DialogResult.Cancel;
            cancel.Location = new Point(268, 56);
            cancel.Width = 80;
            f.Controls.Add(tb);
            f.Controls.Add(ok);
            f.Controls.Add(cancel);
            f.AcceptButton = ok;
            f.CancelButton = cancel;
            return f.ShowDialog() == DialogResult.OK ? tb.Text : null;
        }
    }
}

sealed class MenuHost : Form
{
    const int WM_HOTKEY = 0x0312;
    const int HOTKEY_ID = 1;
    const uint MOD_ALT = 0x0001;
    const uint MOD_WIN = 0x0008;
    const int VK_X = 0x58;
    const byte VK_NOP = 0xE8;
    const int ColWidth = 240;
    const int PadWidth = 320;
    const int HeaderHeight = 34;
    const int RowHeight = 28;
    const int MinHeight = 340;

    [DllImport("user32.dll")] static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);
    [DllImport("user32.dll")] static extern bool UnregisterHotKey(IntPtr hWnd, int id);
    [DllImport("user32.dll")] static extern void keybd_event(byte bVk, byte bScan, uint dwFlags, UIntPtr dwExtraInfo);
    [DllImport("user32.dll")] static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] static extern uint GetWindowThreadProcessId(IntPtr hWnd, IntPtr lpdwProcessId);
    [DllImport("user32.dll")] static extern bool AttachThreadInput(uint idAttach, uint idAttachTo, bool fAttach);
    [DllImport("user32.dll")] static extern bool SetForegroundWindow(IntPtr hWnd);
    [DllImport("kernel32.dll")] static extern uint GetCurrentThreadId();
    [DllImport("user32.dll")] static extern IntPtr WindowFromPoint(System.Drawing.Point p);
    [DllImport("user32.dll")] static extern IntPtr GetAncestor(IntPtr hwnd, uint flags);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern int GetClassName(IntPtr hwnd, StringBuilder name, int max);

    static readonly Point Parked = new Point(-32000, -32000);

    List<Entry> entries;                       // everything, including hidden entries, in order
    readonly List<Entry> shown = new List<Entry>();
    string folderUnderMouse;                   // folder under the mouse when the menu opened
    readonly System.Windows.Forms.Timer timer = new System.Windows.Forms.Timer();
    readonly NotifyIcon tray = new NotifyIcon();
    readonly Font font = new Font("Segoe UI", 11f);
    readonly Font headFont = new Font("Segoe UI", 8.5f);
    readonly Label title = new Label();
    readonly TextBox pad = new TextBox();
    readonly Label padStatus = new Label();
    readonly Button copyBtn = new Button();
    readonly Button clearBtn = new Button();
    SettingsForm settings;
    bool open;
    int hover = -1;
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

        timer.Interval = 1000;
        timer.Tick += delegate { OnTimer(); };

        tray.Icon = SystemIcons.Application;
        tray.Text = "WinTools Menu (Win+Alt+X)";
        tray.Visible = true;
        ContextMenuStrip cm = new ContextMenuStrip();
        cm.Items.Add("Custom shortcuts (Win+Alt+X twice)", null, delegate { ShowSettings(); });
        cm.Items.Add("Exit WinTools Menu", null, delegate { Application.Exit(); });
        tray.ContextMenuStrip = cm;

        // Parked off-screen while closed. Hiding the window made Windows stop delivering the hotkey.
        Location = Parked;
        IntPtr handle = Handle;
        if (!RegisterHotKey(handle, HOTKEY_ID, MOD_WIN | MOD_ALT, VK_X))
        {
            tray.Text = "WinTools Menu: Win+Alt+X is used by another program";
        }
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

    int RowCount() { return Math.Max(1, (shown.Count + 1) / 2); }

    void LayoutMenu()
    {
        int rows = RowCount();
        int listW = 2 * ColWidth;
        int height = Math.Max(HeaderHeight + rows * RowHeight + 8, MinHeight);
        ClientSize = new Size(listW + 12 + PadWidth + 12, height);
        int px = listW + 12;
        title.Location = new Point(px, 8);
        pad.Location = new Point(px, HeaderHeight);
        pad.Size = new Size(PadWidth, height - HeaderHeight - 74);
        padStatus.Location = new Point(px, height - 66);
        copyBtn.Location = new Point(px, height - 40);
        clearBtn.Location = new Point(px + PadWidth - 110, height - 40);
    }

    void BuildPad()
    {
        title.Text = "Plain text: paste here, then copy";
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

        copyBtn.Text = "Copy plain text";
        copyBtn.Size = new Size(170, 30);
        copyBtn.TabStop = false;
        copyBtn.Click += delegate { CopyPlain(); };
        Controls.Add(copyBtn);

        clearBtn.Text = "Clear";
        clearBtn.Size = new Size(110, 30);
        clearBtn.TabStop = false;
        clearBtn.Click += delegate { pad.Clear(); pad.Focus(); };
        Controls.Add(clearBtn);
    }

    // Copies the pad text, which is plain text only, to the clipboard. A few retries because another
    // program may hold the clipboard for a moment.
    void CopyPlain()
    {
        string text = pad.Text;
        if (text.Length == 0) { padStatus.Text = "Nothing to copy"; return; }
        for (int attempt = 0; attempt < 5; attempt++)
        {
            try
            {
                Clipboard.SetText(text, TextDataFormat.UnicodeText);
                padStatus.Text = "Copied " + text.Length + " characters";
                return;
            }
            catch (Exception)
            {
                Thread.Sleep(100);
            }
        }
        padStatus.Text = "Could not use the clipboard. Try again.";
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

    bool ItemEnabled(Entry e) { return !e.NeedsFolder || folderUnderMouse != null; }

    // The folder shown by the Explorer window under the mouse, or the desktop. Null if it is not a folder.
    static string FolderUnderCursor()
    {
        try
        {
            IntPtr top = GetAncestor(WindowFromPoint(Cursor.Position), 2); // GA_ROOT
            StringBuilder cls = new StringBuilder(256);
            GetClassName(top, cls, 256);
            string c = cls.ToString();
            if (c == "Progman" || c == "WorkerW") return Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
            if (c != "CabinetWClass") return null;
            return ExplorerLocation(top);
        }
        catch (Exception)
        {
            return null;
        }
    }

    // Asks Explorer (Shell.Application) which folder the window with this handle shows.
    static string ExplorerLocation(IntPtr hwnd)
    {
        Type t = Type.GetTypeFromProgID("Shell.Application");
        if (t == null) return null;
        object shell = Activator.CreateInstance(t);
        object windows = t.InvokeMember("Windows", System.Reflection.BindingFlags.InvokeMethod, null, shell, null);
        int count = (int)windows.GetType().InvokeMember("Count", System.Reflection.BindingFlags.GetProperty, null, windows, null);
        for (int i = 0; i < count; i++)
        {
            object w = windows.GetType().InvokeMember("Item", System.Reflection.BindingFlags.InvokeMethod, null, windows, new object[] { i });
            if (w == null) continue;
            long handle = Convert.ToInt64(w.GetType().InvokeMember("HWND", System.Reflection.BindingFlags.GetProperty, null, w, null));
            if (handle != hwnd.ToInt64()) continue;
            string url = (string)w.GetType().InvokeMember("LocationURL", System.Reflection.BindingFlags.GetProperty, null, w, null);
            if (!string.IsNullOrEmpty(url) && url.StartsWith("file:", StringComparison.OrdinalIgnoreCase))
            {
                return new Uri(url).LocalPath;
            }
            return null;
        }
        return null;
    }

    static string Quote(string path) { return "\"" + path + "\""; }

    // Runs one entry. Folder entries run in the folder under the mouse. Scripts run through their host.
    static void RunEntry(Entry e, string folder)
    {
        string target = Config.Expand(e.Target);
        string args = Config.Expand(e.Args);
        if (e.NeedsFolder)
        {
            if (folder == null) return;
            Start(target, args, folder, false);
            return;
        }
        // Built-in Notepad++ may live under the 32-bit folder instead.
        if (!File.Exists(target) && e.Target != null && e.Target.IndexOf("{programfiles}", StringComparison.OrdinalIgnoreCase) >= 0)
        {
            string alt = Config.Expand(e.Target.Replace("{programfiles}", "{programfiles86}"));
            if (File.Exists(alt)) target = alt;
        }
        string ext = Path.GetExtension(target).ToLowerInvariant();
        if (ext == ".ps1")
        {
            string ps = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "WindowsPowerShell\\v1.0\\powershell.exe");
            Start(ps, "-NoProfile -ExecutionPolicy Bypass -File " + Quote(target) + " " + args, Path.GetDirectoryName(target), false);
        }
        else if (ext == ".bat" || ext == ".cmd")
        {
            string cmd = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "cmd.exe");
            Start(cmd, "/c \"" + Quote(target) + " " + args + "\"", Path.GetDirectoryName(target), false);
        }
        else
        {
            string dir = File.Exists(target) ? Path.GetDirectoryName(target) : null;
            Start(target, args, dir, true);
        }
    }

    static void Start(string file, string args, string dir, bool shell)
    {
        try
        {
            ProcessStartInfo psi = new ProcessStartInfo(file, args ?? "");
            psi.UseShellExecute = shell;
            if (!string.IsNullOrEmpty(dir) && Directory.Exists(dir)) psi.WorkingDirectory = dir;
            Process.Start(psi);
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "WinTools");
        }
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

    void CloseMenu()
    {
        if (!open) return;
        open = false;
        hover = -1;
        timer.Stop();
        Location = Parked;
        // The parked window would otherwise keep the keyboard. Give it back to the window that had it.
        if (previousForeground != IntPtr.Zero && previousForeground != Handle)
        {
            uint fgThread = GetWindowThreadProcessId(previousForeground, IntPtr.Zero);
            uint me = GetCurrentThreadId();
            bool attached = fgThread != 0 && fgThread != me && AttachThreadInput(me, fgThread, true);
            SetForegroundWindow(previousForeground);
            if (attached) AttachThreadInput(me, fgThread, false);
        }
        previousForeground = IntPtr.Zero;
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
        if (e.KeyCode >= Keys.A && e.KeyCode <= Keys.Z)
        {
            char c = (char)('A' + (int)(e.KeyCode - Keys.A));
            foreach (Entry en in shown)
            {
                if (en.Letter == c.ToString() && ItemEnabled(en))
                {
                    e.SuppressKeyPress = true;
                    CloseMenu();
                    BeginInvoke(new Action(delegate { RunEntry(en, folderUnderMouse); }));
                    return;
                }
            }
        }
    }

    void OnTimer()
    {
        if ((DateTime.Now - lastKey).TotalSeconds > 60) CloseMenu();
    }

    // Column-major: the first column holds the first RowCount() entries. Null if the spot is empty.
    int ItemAt(int x, int y)
    {
        if (x < 0 || x >= 2 * ColWidth || y < HeaderHeight) return -1;
        int rows = RowCount();
        int col = x / ColWidth;
        int row = (y - HeaderHeight) / RowHeight;
        if (row >= rows) return -1;
        int index = col * rows + row;
        return index < shown.Count ? index : -1;
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        int h = ItemAt(e.X, e.Y);
        if (h != hover)
        {
            hover = h;
            Invalidate();
        }
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        if (!open || e.Button != MouseButtons.Left) return;
        int i = ItemAt(e.X, e.Y);
        if (i < 0 || !ItemEnabled(shown[i])) return;
        Entry en = shown[i];
        CloseMenu();
        BeginInvoke(new Action(delegate { RunEntry(en, folderUnderMouse); }));
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        Graphics g = e.Graphics;
        TextRenderer.DrawText(g, "Win+Alt+X again: customise   (press a letter)", headFont,
            new Rectangle(10, 8, 2 * ColWidth - 20, 18), Color.FromArgb(156, 163, 175), TextFormatFlags.NoPadding);
        int rows = RowCount();
        for (int i = 0; i < shown.Count; i++)
        {
            int col = i / rows;
            int row = i % rows;
            Rectangle r = new Rectangle(col * ColWidth, HeaderHeight + row * RowHeight, ColWidth, RowHeight);
            if (i == hover)
            {
                using (SolidBrush b = new SolidBrush(Color.FromArgb(55, 65, 81)))
                {
                    g.FillRectangle(b, r);
                }
            }
            DrawItem(g, shown[i], r);
        }
    }

    void DrawItem(Graphics g, Entry en, Rectangle r)
    {
        TextFormatFlags flags = TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine | TextFormatFlags.VerticalCenter;
        Rectangle textRect = new Rectangle(r.X + 14, r.Y, r.Width - 14, r.Height);
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
        UnregisterHotKey(Handle, HOTKEY_ID);
        tray.Visible = false;
        tray.Dispose();
        base.OnFormClosed(e);
    }
}

public sealed class SteamGame
{
    public string AppId;
    public string Name;
}

// Finds installed Steam games by reading Steam's own files (libraryfolders.vdf and appmanifest_*.acf).
static class Steam
{
    public static string SteamRoot()
    {
        string p = null;
        try
        {
            using (Microsoft.Win32.RegistryKey k = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(@"Software\Valve\Steam"))
            {
                if (k != null) p = k.GetValue("SteamPath") as string;
            }
        }
        catch (Exception) { }
        if (string.IsNullOrEmpty(p))
        {
            try
            {
                using (Microsoft.Win32.RegistryKey k = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(@"SOFTWARE\WOW6432Node\Valve\Steam"))
                {
                    if (k != null) p = k.GetValue("InstallPath") as string;
                }
            }
            catch (Exception) { }
        }
        if (string.IsNullOrEmpty(p)) return null;
        string full = Path.GetFullPath(p.Replace('/', '\\'));
        return Directory.Exists(full) ? full : null;
    }

    // The main Steam folder plus every extra library listed in libraryfolders.vdf.
    public static List<string> LibraryFolders(string root)
    {
        List<string> libs = new List<string>();
        libs.Add(root);
        string vdf = Path.Combine(root, "steamapps", "libraryfolders.vdf");
        if (!File.Exists(vdf)) return libs;
        foreach (Match m in Regex.Matches(File.ReadAllText(vdf), "\"path\"\\s+\"([^\"]+)\""))
        {
            string p = m.Groups[1].Value.Replace("\\\\", "\\");
            if (!Directory.Exists(p)) continue;
            bool known = false;
            foreach (string l in libs)
            {
                if (string.Equals(l, p, StringComparison.OrdinalIgnoreCase)) { known = true; break; }
            }
            if (!known) libs.Add(p);
        }
        return libs;
    }

    public static List<SteamGame> Installed()
    {
        List<SteamGame> games = new List<SteamGame>();
        string root = SteamRoot();
        if (root == null) return games;
        foreach (string lib in LibraryFolders(root))
        {
            string apps = Path.Combine(lib, "steamapps");
            if (!Directory.Exists(apps)) continue;
            foreach (string f in Directory.GetFiles(apps, "appmanifest_*.acf"))
            {
                string text = File.ReadAllText(f);
                Match id = Regex.Match(text, "\"appid\"\\s+\"(\\d+)\"");
                Match name = Regex.Match(text, "\"name\"\\s+\"([^\"]+)\"");
                if (!id.Success || !name.Success) continue;
                string appId = id.Groups[1].Value;
                string title = name.Groups[1].Value;
                // Steam's own tools are not games. Skip them, and skip an app listed twice.
                if (title.StartsWith("Proton", StringComparison.OrdinalIgnoreCase)) continue;
                if (title.StartsWith("Steam Linux Runtime", StringComparison.OrdinalIgnoreCase)) continue;
                if (title.StartsWith("Steamworks Common", StringComparison.OrdinalIgnoreCase)) continue;
                bool seen = false;
                foreach (SteamGame g in games)
                {
                    if (g.AppId == appId) { seen = true; break; }
                }
                if (!seen) games.Add(new SteamGame { AppId = appId, Name = title });
            }
        }
        games.Sort(delegate(SteamGame a, SteamGame b) { return string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase); });
        return games;
    }
}

static class SteamPicker
{
    // Shows the installed games. Returns the picked ones, or an empty list.
    public static List<SteamGame> Pick(IWin32Window owner)
    {
        List<SteamGame> games = Steam.Installed();
        List<SteamGame> result = new List<SteamGame>();
        using (Form f = new Form())
        {
            f.Text = "Add a Steam game";
            f.FormBorderStyle = FormBorderStyle.FixedDialog;
            f.StartPosition = FormStartPosition.CenterParent;
            f.MaximizeBox = false;
            f.MinimizeBox = false;
            f.ClientSize = new Size(420, 420);

            Label info = new Label();
            info.Text = games.Count == 0
                ? "No installed Steam games were found. Is Steam installed here?"
                : "Pick one or more games (Ctrl+click). They start through Steam.";
            info.Location = new Point(12, 10);
            info.AutoSize = true;
            f.Controls.Add(info);

            ListView lv = new ListView();
            lv.View = View.Details;
            lv.FullRowSelect = true;
            lv.MultiSelect = true;
            lv.HideSelection = false;
            lv.Location = new Point(12, 36);
            lv.Size = new Size(396, 330);
            lv.Columns.Add("Game", 300);
            lv.Columns.Add("App ID", 80);
            foreach (SteamGame g in games)
            {
                ListViewItem it = new ListViewItem(g.Name);
                it.SubItems.Add(g.AppId);
                it.Tag = g;
                lv.Items.Add(it);
            }
            f.Controls.Add(lv);

            Button ok = new Button();
            ok.Text = "Add";
            ok.DialogResult = DialogResult.OK;
            ok.Location = new Point(236, 376);
            ok.Size = new Size(80, 30);
            Button cancel = new Button();
            cancel.Text = "Cancel";
            cancel.DialogResult = DialogResult.Cancel;
            cancel.Location = new Point(328, 376);
            cancel.Size = new Size(80, 30);
            f.Controls.Add(ok);
            f.Controls.Add(cancel);
            f.AcceptButton = ok;
            f.CancelButton = cancel;

            if (f.ShowDialog(owner) == DialogResult.OK)
            {
                foreach (ListViewItem it in lv.SelectedItems) result.Add((SteamGame)it.Tag);
            }
        }
        return result;
    }
}

// Custom Shortcuts: untick to hide, rename, move, add files (or drag them in). Save writes menu.json.
sealed class SettingsForm : Form
{
    readonly MenuHost host;
    readonly List<Entry> work;
    readonly ListView list = new ListView();
    readonly Label hint = new Label();

    public SettingsForm(MenuHost host, List<Entry> entries)
    {
        this.host = host;
        work = entries;
        Text = "WinTools Custom Shortcuts";
        Font = new Font("Segoe UI", 9.5f);
        StartPosition = FormStartPosition.CenterScreen;
        ClientSize = new Size(820, 540);
        KeyPreview = true;
        AllowDrop = true;
        DragEnter += OnDragEnter;
        DragDrop += OnDragDrop;

        hint.Text = "Untick to hide an entry. Drag files here to add them. Letters are picked automatically.";
        hint.Location = new Point(12, 10);
        hint.AutoSize = true;
        Controls.Add(hint);

        list.View = View.Details;
        list.FullRowSelect = true;
        list.CheckBoxes = true;
        list.HideSelection = false;
        list.Location = new Point(12, 36);
        list.Size = new Size(796, 400);
        list.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
        list.Columns.Add("Key", 50);
        list.Columns.Add("Name", 240);
        list.Columns.Add("Target", 400);
        list.Columns.Add("Type", 80);
        list.AllowDrop = true;
        list.DragEnter += OnDragEnter;
        list.DragDrop += OnDragDrop;
        list.ItemChecked += delegate(object s, ItemCheckedEventArgs e) { ((Entry)e.Item.Tag).Hidden = !e.Item.Checked; };
        Controls.Add(list);

        AddButton("Up", 12, delegate { MoveSelected(-1); });
        AddButton("Down", 96, delegate { MoveSelected(1); });
        AddButton("Rename", 180, delegate { Rename(); });
        AddButton("Add files...", 264, delegate { AddWithDialog(); });
        AddButton("Remove custom", 372, delegate { RemoveCustom(); });
        AddButton("Reset to built-in", 500, delegate { ResetAll(); });
        AddButton("Cancel", 634, delegate { Close(); });
        AddButton("Save", 718, delegate { Save(); });

        Button steamBtn = new Button();
        steamBtn.Text = "Add Steam game...";
        steamBtn.Location = new Point(12, 492);
        steamBtn.Size = new Size(160, 30);
        steamBtn.Anchor = AnchorStyles.Bottom | AnchorStyles.Left;
        steamBtn.Click += delegate { AddSteam(); };
        Controls.Add(steamBtn);

        Refresh(-1);
    }

    // Lists the installed Steam games and adds the picked ones as steam://rungameid/<id> entries.
    void AddSteam()
    {
        List<SteamGame> picked = SteamPicker.Pick(this);
        bool added = false;
        foreach (SteamGame g in picked)
        {
            string target = "steam://rungameid/" + g.AppId;
            bool exists = false;
            foreach (Entry e in work)
            {
                if (string.Equals(e.Target, target, StringComparison.OrdinalIgnoreCase)) { exists = true; break; }
            }
            if (exists) continue;
            work.Add(new Entry
            {
                Id = "steam" + g.AppId,
                Custom = true,
                Title = g.Name,
                Target = target,
                Args = "",
                Letter = AutoLetter(g.Name),
                Hidden = false,
                NeedsFolder = false
            });
            added = true;
        }
        if (added) Refresh(work.Count - 1);
    }

    void AddButton(string text, int x, EventHandler click)
    {
        Button b = new Button();
        b.Text = text;
        b.Location = new Point(x, 452);
        b.Size = new Size(text == "Remove custom" || text == "Reset to built-in" ? 116 : 80, 30);
        b.Anchor = AnchorStyles.Bottom | AnchorStyles.Left;
        b.Click += click;
        Controls.Add(b);
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.KeyCode == Keys.Escape) Close();
    }

    int Selected()
    {
        return list.SelectedIndices.Count > 0 ? list.SelectedIndices[0] : -1;
    }

    void Refresh(int select)
    {
        list.BeginUpdate();
        list.Items.Clear();
        foreach (Entry e in work)
        {
            ListViewItem it = new ListViewItem(string.IsNullOrEmpty(e.Letter) ? "-" : e.Letter);
            it.Tag = e;
            it.Checked = !e.Hidden;
            it.SubItems.Add(e.Title ?? "");
            it.SubItems.Add(e.Custom ? (e.Target ?? "") : (Config.Expand(e.Target) + " " + Config.Expand(e.Args)).Trim());
            it.SubItems.Add(e.Custom ? "Custom" : "Built-in");
            list.Items.Add(it);
        }
        list.EndUpdate();
        if (select >= 0 && select < list.Items.Count)
        {
            list.Items[select].Selected = true;
            list.Items[select].Focused = true;
            list.EnsureVisible(select);
        }
    }

    void MoveSelected(int delta)
    {
        int i = Selected();
        int j = i + delta;
        if (i < 0 || j < 0 || j >= work.Count) return;
        Entry tmp = work[i];
        work[i] = work[j];
        work[j] = tmp;
        Refresh(j);
    }

    void Rename()
    {
        int i = Selected();
        if (i < 0) return;
        string v = Dialogs.Prompt("Rename", work[i].Title);
        if (v != null && v.Trim().Length > 0)
        {
            work[i].Title = v.Trim();
            Refresh(i);
        }
    }

    void AddWithDialog()
    {
        using (OpenFileDialog dlg = new OpenFileDialog())
        {
            dlg.Title = "Add programs, scripts or shortcuts";
            dlg.Filter = "Programs and scripts|*.exe;*.bat;*.cmd;*.ps1;*.lnk;*.url;*.msc;*.cpl|All files|*.*";
            dlg.Multiselect = true;
            if (dlg.ShowDialog(this) == DialogResult.OK)
            {
                foreach (string f in dlg.FileNames) AddFile(f);
                Refresh(work.Count - 1);
            }
        }
    }

    void AddFile(string path)
    {
        string name = Path.GetFileNameWithoutExtension(path);
        work.Add(new Entry
        {
            Id = "c" + Guid.NewGuid().ToString("N"),
            Custom = true,
            Title = name,
            Target = path,
            Args = "",
            Letter = AutoLetter(name),
            Hidden = false,
            NeedsFolder = false
        });
    }

    // First letter of the name that no entry uses yet, otherwise the first free letter A-Z.
    string AutoLetter(string name)
    {
        bool[] used = new bool[26];
        foreach (Entry e in work)
        {
            if (!string.IsNullOrEmpty(e.Letter))
            {
                char c = char.ToUpperInvariant(e.Letter[0]);
                if (c >= 'A' && c <= 'Z') used[c - 'A'] = true;
            }
        }
        foreach (char ch in name)
        {
            char c = char.ToUpperInvariant(ch);
            if (c >= 'A' && c <= 'Z' && !used[c - 'A']) return c.ToString();
        }
        for (int i = 0; i < 26; i++)
        {
            if (!used[i]) return ((char)('A' + i)).ToString();
        }
        return "";
    }

    void RemoveCustom()
    {
        int i = Selected();
        if (i < 0 || !work[i].Custom) return;
        work.RemoveAt(i);
        Refresh(Math.Min(i, work.Count - 1));
    }

    void ResetAll()
    {
        if (MessageBox.Show(this, "Put the built-in entries back as they were? Custom entries are removed.", "WinTools",
                MessageBoxButtons.OKCancel, MessageBoxIcon.Warning) != DialogResult.OK) return;
        work.Clear();
        work.AddRange(Config.Defaults());
        Refresh(-1);
    }

    void Save()
    {
        Config.Save(work);
        host.Reload(work);
        Close();
    }

    void OnDragEnter(object sender, DragEventArgs e)
    {
        e.Effect = e.Data.GetDataPresent(DataFormats.FileDrop) ? DragDropEffects.Copy : DragDropEffects.None;
    }

    void OnDragDrop(object sender, DragEventArgs e)
    {
        string[] files = (string[])e.Data.GetData(DataFormats.FileDrop);
        if (files == null || files.Length == 0) return;
        foreach (string f in files) AddFile(f);
        Refresh(work.Count - 1);
    }
}
