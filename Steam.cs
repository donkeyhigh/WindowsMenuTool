// Installed Steam games (from the Steam library folders) and the game picker dialog.
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
