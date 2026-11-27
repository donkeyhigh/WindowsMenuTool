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

// Offers to install missing tools: Notepad++ and Sandboxie-Plus with winget, Windows Sandbox as a Windows feature.
// Nothing is downloaded or changed unless the user answers Yes. Long work runs on a background thread.
static class Installers
{
    const string Caption = "WinTools";

    static string WingetPath()
    {
        string p = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Microsoft\\WindowsApps\\winget.exe");
        return File.Exists(p) ? p : null;
    }

    static bool Ask(string text)
    {
        return MessageBox.Show(text, Caption, MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes;
    }

    static void Tell(string text, MessageBoxIcon icon)
    {
        MessageBox.Show(text, Caption, MessageBoxButtons.OK, icon);
    }

    static void Open(string fileOrUrl, string args)
    {
        try
        {
            ProcessStartInfo psi = new ProcessStartInfo(fileOrUrl, args ?? "");
            psi.UseShellExecute = true;
            Process.Start(psi);
        }
        catch (Exception ex) { Tell(ex.Message, MessageBoxIcon.Warning); }
    }

    // Runs a program and waits for it. Returns its exit code, or -1 if it could not start (for example UAC was refused).
    static int RunAndWait(string file, string args, bool elevate)
    {
        try
        {
            ProcessStartInfo psi = new ProcessStartInfo(file, args);
            psi.UseShellExecute = true;
            if (elevate) psi.Verb = "runas";
            using (Process p = Process.Start(psi))
            {
                if (p == null) return -1;
                p.WaitForExit();
                return p.ExitCode;
            }
        }
        catch (Exception) { return -1; }
    }

    static void InBackground(ThreadStart work)
    {
        Thread t = new Thread(delegate()
        {
            try { work(); }
            catch (Exception ex) { Tell(ex.Message, MessageBoxIcon.Warning); }
        });
        t.IsBackground = true;
        t.SetApartmentState(ApartmentState.STA);
        t.Start();
    }

    // winget asks for admin rights itself when the installer needs them, and shows its progress in a console window.
    static int Winget(string id)
    {
        return RunAndWait(WingetPath(), "install --id " + id + " -e --source winget --silent"
            + " --accept-package-agreements --accept-source-agreements", false);
    }

    // ---- Notepad++ ----

    public static string NotepadPPPath()
    {
        foreach (Environment.SpecialFolder f in new Environment.SpecialFolder[] { Environment.SpecialFolder.ProgramFiles, Environment.SpecialFolder.ProgramFilesX86 })
        {
            string p = Path.Combine(Environment.GetFolderPath(f), "Notepad++\\notepad++.exe");
            if (File.Exists(p)) return p;
        }
        return null;
    }

    public static void OfferNotepadPP()
    {
        if (!Ask("Notepad++ is not installed.\n\nInstall it now? It is downloaded from the official source and installed "
            + "automatically. Windows asks for permission first.\n\n"
            + "Of course you can also install it yourself if you would rather do that: this shortcut finds it in the "
            + "usual folder, or you can add it to the list in Custom Shortcuts.")) return;
        InBackground(delegate
        {
            if (WingetPath() != null) Winget("Notepad++.Notepad++");
            if (NotepadPPPath() == null) InstallNotepadPPFromGitHub();
            string exe = NotepadPPPath();
            if (exe != null)
            {
                Open(exe, "");
                return;
            }
            if (Ask("Notepad++ was not installed.\n\nOpen the Notepad++ download page instead?"))
                Open("https://notepad-plus-plus.org/downloads/", "");
        });
    }

    // Without winget: the newest installer from the official GitHub releases, run silently (/S).
    static void InstallNotepadPPFromGitHub()
    {
        System.Net.ServicePointManager.SecurityProtocol |= (System.Net.SecurityProtocolType)3072; // TLS 1.2
        string arch = Environment.GetEnvironmentVariable("PROCESSOR_ARCHITEW6432") ?? Environment.GetEnvironmentVariable("PROCESSOR_ARCHITECTURE") ?? "";
        string ending = string.Equals(arch, "ARM64", StringComparison.OrdinalIgnoreCase) ? ".Installer.arm64.exe"
            : Environment.Is64BitOperatingSystem ? ".Installer.x64.exe" : ".Installer.exe";
        string url = null;
        using (System.Net.WebClient wc = new System.Net.WebClient())
        {
            wc.Headers.Add("User-Agent", "WinToolsMenu");
            string json = wc.DownloadString("https://api.github.com/repos/notepad-plus-plus/notepad-plus-plus/releases/latest");
            Dictionary<string, object> release = new JavaScriptSerializer().Deserialize<Dictionary<string, object>>(json);
            System.Collections.IEnumerable assets = release.ContainsKey("assets") ? release["assets"] as System.Collections.IEnumerable : null;
            if (assets == null) return;
            foreach (object a in assets)
            {
                Dictionary<string, object> asset = a as Dictionary<string, object>;
                if (asset == null) continue;
                string name = asset["name"] as string;
                if (name != null && name.EndsWith(ending, StringComparison.OrdinalIgnoreCase))
                {
                    url = asset["browser_download_url"] as string;
                    break;
                }
            }
            if (url == null) return;
            string file = Path.Combine(Path.GetTempPath(), "npp-installer.exe");
            wc.DownloadFile(url, file);
            RunAndWait(file, "/S", true);
            try { File.Delete(file); } catch (Exception) { }
        }
    }

    // ---- Windows Sandbox / Sandboxie-Plus ----

    // Sandboxie-Plus (SandMan.exe) or classic Sandboxie (SbieCtrl.exe) in the usual folders, Plus first. Null if none.
    public static string SandboxiePath()
    {
        string[] files = { "Sandboxie-Plus\\SandMan.exe", "Sandboxie\\SandMan.exe", "Sandboxie-Plus\\SbieCtrl.exe", "Sandboxie\\SbieCtrl.exe" };
        foreach (string f in files)
        {
            foreach (Environment.SpecialFolder root in new Environment.SpecialFolder[] { Environment.SpecialFolder.ProgramFiles, Environment.SpecialFolder.ProgramFilesX86 })
            {
                string p = Path.Combine(Environment.GetFolderPath(root), f);
                if (File.Exists(p)) return p;
            }
        }
        return null;
    }

    // "Sandboxie Plus" for SandMan.exe, "Sandboxie" for the classic SbieCtrl.exe.
    public static string SandboxieName(string path)
    {
        return string.Equals(Path.GetFileName(path), "SandMan.exe", StringComparison.OrdinalIgnoreCase) ? "Sandboxie Plus" : "Sandboxie";
    }

    // One sentence on why Windows Sandbox does not start on this PC.
    public static string WhyNoWindowsSandbox()
    {
        Cursor.Current = Cursors.WaitCursor;
        bool home = IsHomeEdition();
        int state = home ? 3 : SandboxFeatureState();
        Cursor.Current = Cursors.Default;
        if (home) return "Windows Sandbox does not work on this PC: Windows Home does not have it (it needs Pro, Enterprise or Education).";
        if (state == 3) return "Windows Sandbox does not work on this PC: this Windows does not have it.";
        if (state == 1) return "Windows Sandbox is turned on, but it only works after the PC has restarted.";
        if (state == 2) return "Windows Sandbox is not turned on on this PC.";
        return "Windows Sandbox was not found on this PC.";
    }

    // Windows Home is "Core..." in EditionID. Home has no Windows Sandbox.
    static bool IsHomeEdition()
    {
        try
        {
            object v = Microsoft.Win32.Registry.GetValue("HKEY_LOCAL_MACHINE\\SOFTWARE\\Microsoft\\Windows NT\\CurrentVersion", "EditionID", null);
            string id = v as string;
            return id != null && id.StartsWith("Core", StringComparison.OrdinalIgnoreCase);
        }
        catch (Exception) { return false; }
    }

    // InstallState of the Windows Sandbox feature: 1 on, 2 off, 3 not available in this Windows, 0 unknown.
    static int SandboxFeatureState()
    {
        try
        {
            using (ManagementObjectSearcher s = new ManagementObjectSearcher(
                "SELECT InstallState FROM Win32_OptionalFeature WHERE Name='Containers-DisposableClientVM'"))
            {
                foreach (ManagementObject o in s.Get()) return Convert.ToInt32(o["InstallState"]);
            }
            return 3;
        }
        catch (Exception) { return 0; }
    }

    // True if virtualization is on in the BIOS/UEFI, or a hypervisor already runs (then the CPU reports it as off).
    static bool VirtualizationOn()
    {
        try
        {
            using (ManagementObjectSearcher s = new ManagementObjectSearcher("SELECT HypervisorPresent FROM Win32_ComputerSystem"))
            {
                foreach (ManagementObject o in s.Get()) if (o["HypervisorPresent"] is bool && (bool)o["HypervisorPresent"]) return true;
            }
            using (ManagementObjectSearcher s = new ManagementObjectSearcher("SELECT VirtualizationFirmwareEnabled FROM Win32_Processor"))
            {
                foreach (ManagementObject o in s.Get()) if (o["VirtualizationFirmwareEnabled"] is bool && (bool)o["VirtualizationFirmwareEnabled"]) return true;
            }
            return false;
        }
        catch (Exception) { return true; }
    }

    static void OfferRestart(string text)
    {
        if (Ask(text + "\n\nRestart the PC now? Save your work first.")) Open("shutdown.exe", "/r /t 0");
    }

    // Windows Sandbox was not found. Explains why and offers what fits this PC.
    // switchTo(name) is called on this thread when Sandboxie-Plus was installed, so the shortcut can be renamed.
    public static void SandboxMissing(Action<string> switchTo)
    {
        Cursor.Current = Cursors.WaitCursor;
        bool home = IsHomeEdition();
        int state = home ? 3 : SandboxFeatureState();
        Cursor.Current = Cursors.Default;

        if (home || state == 3)
        {
            string why = home ? "Your Windows is a Home edition, which does not support Windows Sandbox."
                : "This Windows does not support Windows Sandbox.";
            if (!Ask(why + " It needs Windows 11 Pro, Enterprise or Education.\n\n"
                + "Install Sandboxie-Plus instead? It is free and runs programs in an isolated box. "
                + "It is downloaded and installed automatically, and this shortcut then opens it.\n\n"
                + "Of course you can also install it yourself (or another sandbox program) and add it to the list in "
                + "Custom Shortcuts if you would rather do that.")) return;
            if (WingetPath() == null)
            {
                Tell("winget (App Installer) is missing, so Sandboxie-Plus cannot be installed automatically. "
                    + "The download page opens now.", MessageBoxIcon.Information);
                Open("https://sandboxie-plus.com/downloads/", "");
                return;
            }
            InBackground(delegate
            {
                Winget("Sandboxie.Plus");
                string exe = SandboxiePath();
                if (exe != null)
                {
                    if (switchTo != null) switchTo(SandboxieName(exe));
                    Open(exe, "");
                    return;
                }
                if (Ask("Sandboxie-Plus was not installed.\n\nOpen the Sandboxie-Plus download page instead?"))
                    Open("https://sandboxie-plus.com/downloads/", "");
            });
            return;
        }
        if (state == 1)
        {
            OfferRestart("Windows Sandbox is turned on, but Windows finishes setting it up when the PC restarts.");
            return;
        }
        if (!VirtualizationOn())
        {
            Tell("Windows Sandbox needs virtualization, which is turned off in this PC's BIOS/UEFI.\n\n"
                + "Restart into the BIOS/UEFI setup and turn on Intel VT-x (Intel Virtualization Technology) or AMD SVM. "
                + "Then use this shortcut again.", MessageBoxIcon.Information);
            return;
        }
        if (!Ask("Windows Sandbox is not turned on.\n\nTurn it on now? Windows asks for permission, then installs it. "
            + "The PC must restart afterwards.")) return;
        InBackground(delegate
        {
            string dism = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "dism.exe");
            int code = RunAndWait(dism, "/online /enable-feature /featurename:Containers-DisposableClientVM /all /norestart", true);
            if (code == 0 || code == 3010) OfferRestart("Windows Sandbox is turned on.");
            else if (code == -1) Tell("Windows Sandbox was not turned on (permission was not given).", MessageBoxIcon.Information);
            else Tell("Windows Sandbox could not be turned on (DISM error " + code + ").\n\n"
                + "Try \"Turn Windows features on or off\" (optionalfeatures.exe) instead.", MessageBoxIcon.Warning);
        });
    }
}
