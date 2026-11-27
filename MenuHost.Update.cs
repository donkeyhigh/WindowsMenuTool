// Update checks against GitHub, installing updates, View on GitHub and Uninstall.
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
    static string PowerShellExe()
    {
        return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "WindowsPowerShell\\v1.0\\powershell.exe");
    }

    static bool IsNewer(string latest, string current)
    {
        try { return new Version(latest) > new Version(current); }
        catch (Exception) { return false; }
    }

    // Reads a small text file from GitHub. Throws if it cannot be reached.
    static string FetchText(string url)
    {
        System.Net.ServicePointManager.SecurityProtocol |= (System.Net.SecurityProtocolType)3072; // TLS 1.2
        System.Net.HttpWebRequest req = (System.Net.HttpWebRequest)System.Net.WebRequest.Create(url);
        req.Timeout = 15000;
        req.UserAgent = "WinToolsMenu";
        using (System.Net.HttpWebResponse res = (System.Net.HttpWebResponse)req.GetResponse())
        using (StreamReader sr = new StreamReader(res.GetResponseStream(), Encoding.UTF8))
        {
            return sr.ReadToEnd();
        }
    }

    // Runs in the background. Asks GitHub for the VERSION file.
    void CheckForUpdates()
    {
        if (checkingUpdates) return;
        checkingUpdates = true;
        lastUpdateCheck = DateTime.Now;
        ThreadPool.QueueUserWorkItem(delegate
        {
            string latest = null;
            try
            {
                string v = FetchText(RawBase + "VERSION");
                if (v != null) latest = v.Trim();
            }
            catch (Exception)
            {
                // Offline or GitHub unreachable: the menu just shows no update.
            }
            string foundLatest = latest;
            try { BeginInvoke(new Action(delegate { ApplyCheck(foundLatest); })); }
            catch (Exception) { }
        });
    }

    void ApplyCheck(string latest)
    {
        checkingUpdates = false;
        latestVersion = (latest != null && IsNewer(latest, AppVersion)) ? latest : null;
        footerHover = -1;
        LayoutMenu();
        Invalidate();
    }

    // VERSION at the newest commit of main. main/VERSION can be up to 5 minutes old (GitHub's cache), which is fine
    // for the automatic checks but not when the user asks. Uses GitHub's API (60 calls an hour), so only on request.
    static string FetchFreshVersion()
    {
        try
        {
            Dictionary<string, object> commit = new JavaScriptSerializer().Deserialize<Dictionary<string, object>>(FetchText(ApiCommit));
            string sha = commit != null && commit.ContainsKey("sha") ? commit["sha"] as string : null;
            if (!string.IsNullOrEmpty(sha)) return FetchText(RawRoot + sha + "/VERSION").Trim();
        }
        catch (Exception) { }
        return FetchText(RawBase + "VERSION").Trim();
    }

    // From the tray menu and Custom Shortcuts: forgets "Skip this version", checks GitHub now and offers the update.
    public void CheckNowIgnoringSkip()
    {
        Config.Skipped = null;
        try { Config.Save(entries); } catch (Exception) { }
        ThreadPool.QueueUserWorkItem(delegate
        {
            string latest = null;
            try { latest = FetchFreshVersion(); } catch (Exception) { }
            string found = latest;
            try
            {
                BeginInvoke(new Action(delegate
                {
                    ApplyCheck(found);
                    if (found == null)
                    {
                        MessageBox.Show("Could not reach GitHub. Check the internet connection and try again.", "WinTools");
                    }
                    else if (latestVersion == null)
                    {
                        MessageBox.Show("WinTools Menu " + AppVersion + " is the newest version.", "WinTools");
                    }
                    else if (MessageBox.Show("Version " + latestVersion + " is available (you have " + AppVersion + ").\n\nUpdate now?",
                        "WinTools", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
                    {
                        StartUpdate();
                    }
                }));
            }
            catch (Exception) { }
        });
    }

    void SkipCurrentUpdate()
    {
        Config.Skipped = latestVersion;
        try { Config.Save(entries); } catch (Exception) { }
        footerHover = -1;
        LayoutMenu();
        Invalidate();
    }

    // Downloads the program's files from the repo's main branch into a temp folder and runs its install.ps1,
    // which replaces this program and starts the new one. The menu closes and reopens by itself.
    void StartUpdate()
    {
        string tmp = Path.Combine(Path.GetTempPath(), "WindowsMenuTool-" + Guid.NewGuid().ToString("N").Substring(0, 8));
        string ps1 = Path.Combine(Path.GetTempPath(), "WindowsMenuTool-update.ps1");
        File.WriteAllText(ps1,
            "$ErrorActionPreference = 'Stop'\r\n" +
            "try {\r\n" +
            "  [Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12\r\n" +
            "  New-Item -ItemType Directory -Force -Path '" + tmp + "' | Out-Null\r\n" +
            // Only install.ps1 is downloaded here, from the exact newest commit (raw.githubusercontent.com caches
            // main/ for up to 5 minutes). install.ps1 then fetches the other files from the same commit.
            // If GitHub's API fails, main/ is used.
            "  $ref = 'main'\r\n" +
            "  try {\r\n" +
            "    $sha = (Invoke-RestMethod -UseBasicParsing -UserAgent 'WinToolsMenu' -Uri '" + ApiCommit + "').sha\r\n" +
            "    if ($sha) { $ref = $sha }\r\n" +
            "  } catch { }\r\n" +
            "  $installer = Join-Path '" + tmp + "' 'install.ps1'\r\n" +
            "  Invoke-WebRequest -UseBasicParsing -Uri ('" + RawRoot + "' + $ref + '/install.ps1') -OutFile $installer\r\n" +
            "  & $installer -Commit $ref\r\n" +
            "} catch {\r\n" +
            "  Write-Host ('Update failed: ' + $_.Exception.Message)\r\n" +
            "  Read-Host 'Press Enter to close'\r\n" +
            "}\r\n", Encoding.UTF8);
        Start(PowerShellExe(), "-NoProfile -ExecutionPolicy Bypass -File \"" + ps1 + "\"", null, true);
    }

    // Started from the View on GitHub button in Custom Shortcuts.
    public static void OpenGitHub()
    {
        Start("https://github.com/" + RepoOwner + "/" + RepoName, "", null, true);
    }

    // Started from the Uninstall button in Custom Shortcuts.
    public static void UninstallProgram()
    {
        if (MessageBox.Show("Uninstall WinTools Menu? You will be asked separately about your settings.", "WinTools",
                MessageBoxButtons.OKCancel, MessageBoxIcon.Question) != DialogResult.OK) return;
        string script = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "WinToolsMenu", "uninstall.ps1");
        Start(PowerShellExe(), "-NoProfile -ExecutionPolicy Bypass -File \"" + script + "\"", null, true);
    }
}
