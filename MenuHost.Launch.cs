// Running menu items: the folder under the mouse, admin consoles, Windows Sandbox / Sandboxie,
// bringing Settings forward, and starting processes.
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

partial class MenuHost
{
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

    // Closes the menu and runs the entry while the menu still has the foreground, so Windows lets the new
    // window come to the front. Handing the focus back first made Settings and control panels open behind
    // other windows (only flashing in the taskbar). If nothing has taken the focus after a few seconds,
    // it goes back to the window that had it before the menu opened.
    void Launch(Entry en)
    {
        string folder = folderUnderMouse;
        IntPtr previous = previousForeground;
        CloseMenu(false);
        AllowSetForegroundWindow(-1); // ASFW_ANY: also programs started through a broker (Settings, Store apps)
        RunEntry(en, folder);
        System.Windows.Forms.Timer giveBack = new System.Windows.Forms.Timer();
        giveBack.Interval = 3000;
        giveBack.Tick += delegate
        {
            giveBack.Stop();
            giveBack.Dispose();
            if (!open && GetForegroundWindow() == Handle) GiveFocusTo(previous);
        };
        giveBack.Start();
    }

    // Windows Sandbox is WindowsSandbox.exe in System32 on older Windows 11, and an app on newer builds.
    // Without it, an installed Sandboxie (Plus or classic) is used: the first time, the menu says so and renames the
    // shortcut to Sandboxie / Sandboxie Plus. Once renamed, the shortcut opens Sandboxie even if Windows Sandbox appears.
    void RunSandbox(Entry e)
    {
        string sandboxie = Installers.SandboxiePath();
        bool usesSandboxie = (e.Title ?? "").IndexOf("Sandboxie", StringComparison.OrdinalIgnoreCase) >= 0;
        if (usesSandboxie && sandboxie != null)
        {
            Start(sandboxie, "", null, true);
            return;
        }
        string exe = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "WindowsSandbox.exe");
        if (File.Exists(exe))
        {
            Start(exe, "", null, true);
            return;
        }
        string app = null;
        try { app = FindApp("WindowsSandbox", "Windows Sandbox"); } catch (Exception) { }
        if (app != null)
        {
            Start(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "explorer.exe"), "shell:AppsFolder\\" + app, null, true);
            return;
        }
        if (sandboxie != null)
        {
            string name = Installers.SandboxieName(sandboxie);
            MessageBox.Show(Installers.WhyNoWindowsSandbox() + "\n\n"
                + "But " + name + " is already installed here, so this shortcut now opens " + name + " instead. "
                + "You can rename it or move it in Custom Shortcuts.", "WinTools", MessageBoxButtons.OK, MessageBoxIcon.Information);
            SwitchToSandboxie(e, name);
            Start(sandboxie, "", null, true);
            return;
        }
        Installers.SandboxMissing(delegate(string name)
        {
            try { BeginInvoke(new Action(delegate { SwitchToSandboxie(e, name); })); } catch (Exception) { }
        });
    }

    void SwitchToSandboxie(Entry e, string name)
    {
        e.Title = name;
        try { Config.Save(entries); } catch (Exception) { }
        LayoutMenu();
        Invalidate();
    }

    // The launch id (shell:AppsFolder\<id>) of an installed app whose id contains idPart or whose name is name.
    static string FindApp(string idPart, string name)
    {
        Type t = Type.GetTypeFromProgID("Shell.Application");
        if (t == null) return null;
        object shell = Activator.CreateInstance(t);
        object folder = t.InvokeMember("NameSpace", System.Reflection.BindingFlags.InvokeMethod, null, shell, new object[] { "shell:AppsFolder" });
        if (folder == null) return null;
        object items = folder.GetType().InvokeMember("Items", System.Reflection.BindingFlags.InvokeMethod, null, folder, null);
        int count = (int)items.GetType().InvokeMember("Count", System.Reflection.BindingFlags.GetProperty, null, items, null);
        for (int i = 0; i < count; i++)
        {
            object it = items.GetType().InvokeMember("Item", System.Reflection.BindingFlags.InvokeMethod, null, items, new object[] { i });
            if (it == null) continue;
            string path = it.GetType().InvokeMember("Path", System.Reflection.BindingFlags.GetProperty, null, it, null) as string;
            string title = it.GetType().InvokeMember("Name", System.Reflection.BindingFlags.GetProperty, null, it, null) as string;
            if (string.IsNullOrEmpty(path)) continue;
            if (path.IndexOf(idPart, StringComparison.OrdinalIgnoreCase) >= 0
                || string.Equals(title, name, StringComparison.OrdinalIgnoreCase)) return path;
        }
        return null;
    }

    // Runs one entry. Folder entries run in the folder under the mouse. Scripts run through their host.
    void RunEntry(Entry e, string folder)
    {
        if (!e.Custom && e.Id == "sandbox")
        {
            RunSandbox(e);
            return;
        }
        string target = Config.Expand(e.Target);
        string args = Config.Expand(e.Args);
        if (e.NeedsFolder)
        {
            if (folder == null) return;
            // "C:\" would end in \" and escape the closing quote, so a folder never ends in a backslash here.
            string f = folder.TrimEnd('\\');
            if (f.EndsWith(":")) f += "\\.";
            args = (args ?? "").Replace("{folderps}", f.Replace("'", "''")).Replace("{folder}", f);
            if (e.Admin) StartAdmin(target, args);
            else Start(target, args, folder, true);
            return;
        }
        // Built-in Notepad++ may live under the 32-bit folder instead.
        if (!File.Exists(target) && e.Target != null && e.Target.IndexOf("{programfiles}", StringComparison.OrdinalIgnoreCase) >= 0)
        {
            string alt = Config.Expand(e.Target.Replace("{programfiles}", "{programfiles86}"));
            if (File.Exists(alt)) target = alt;
        }
        if (!e.Custom && e.Id == "npp" && !File.Exists(target))
        {
            Installers.OfferNotepadPP();
            return;
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
            if (target.StartsWith("ms-settings:", StringComparison.OrdinalIgnoreCase)) BringSettingsForward();
        }
    }

    // A Settings window that is already open but minimized stays minimized when a settings page is opened
    // in it. For a few seconds after starting a settings page, restore the Settings window and bring it forward.
    static void BringSettingsForward()
    {
        int ticks = 0;
        System.Windows.Forms.Timer t = new System.Windows.Forms.Timer();
        t.Interval = 250;
        t.Tick += delegate
        {
            ticks++;
            IntPtr w = IntPtr.Zero;
            try { w = SettingsWindow(); } catch (Exception) { }
            if (w != IntPtr.Zero)
            {
                if (IsIconic(w)) ShowWindow(w, 9); // SW_RESTORE
                if (GetForegroundWindow() != w) ForceForeground(w);
            }
            bool done = w != IntPtr.Zero && !IsIconic(w) && GetForegroundWindow() == w;
            if (done || ticks >= 20)
            {
                t.Stop();
                t.Dispose();
            }
        };
        t.Start();
    }

    static void ForceForeground(IntPtr hwnd)
    {
        uint fgThread = GetWindowThreadProcessId(GetForegroundWindow(), IntPtr.Zero);
        uint me = GetCurrentThreadId();
        bool attached = fgThread != 0 && fgThread != me && AttachThreadInput(me, fgThread, true);
        SetForegroundWindow(hwnd);
        if (attached) AttachThreadInput(me, fgThread, false);
    }

    // The frame window of the Settings app (SystemSettings.exe), or zero. Its content window (CoreWindow) is a child
    // of the frame while shown, and a separate top-level window with the same title while minimized.
    static IntPtr SettingsWindow()
    {
        Process[] ps = Process.GetProcessesByName("SystemSettings");
        if (ps.Length == 0) return IntPtr.Zero;
        List<uint> pids = new List<uint>();
        foreach (Process p in ps) { pids.Add((uint)p.Id); p.Dispose(); }

        List<IntPtr> frames = new List<IntPtr>();
        List<string> titles = new List<string>();
        EnumWindows(delegate(IntPtr h, IntPtr l)
        {
            StringBuilder cls = new StringBuilder(256);
            GetClassName(h, cls, 256);
            string c = cls.ToString();
            if (c == "ApplicationFrameWindow") frames.Add(h);
            else if (c == "Windows.UI.Core.CoreWindow")
            {
                uint pid;
                GetWindowPid(h, out pid);
                if (pids.Contains(pid))
                {
                    StringBuilder text = new StringBuilder(256);
                    GetWindowText(h, text, 256);
                    if (text.Length > 0) titles.Add(text.ToString());
                }
            }
            return true;
        }, IntPtr.Zero);

        foreach (IntPtr f in frames)
        {
            IntPtr core = FindWindowEx(f, IntPtr.Zero, "Windows.UI.Core.CoreWindow", null);
            if (core == IntPtr.Zero) continue;
            uint pid;
            GetWindowPid(core, out pid);
            if (pids.Contains(pid)) return f;
        }
        foreach (IntPtr f in frames)
        {
            StringBuilder text = new StringBuilder(256);
            GetWindowText(f, text, 256);
            if (text.Length > 0 && titles.Contains(text.ToString())) return f;
        }
        return IntPtr.Zero;
    }

    // Runs as administrator. Windows asks first; answering No is not an error.
    static void StartAdmin(string file, string args)
    {
        try
        {
            ProcessStartInfo psi = new ProcessStartInfo(file, args ?? "");
            psi.UseShellExecute = true;
            psi.Verb = "runas";
            Process.Start(psi);
        }
        catch (System.ComponentModel.Win32Exception ex)
        {
            if (ex.NativeErrorCode != 1223) MessageBox.Show(ex.Message, "WinTools"); // 1223: the user said No
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "WinTools");
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
}
