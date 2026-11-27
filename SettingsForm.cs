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

// Custom Shortcuts: untick to hide, rename, move, add files (or drag them in). Save writes menu.json.
sealed class SettingsForm : Form
{
    readonly MenuHost host;
    readonly List<Entry> work;
    readonly ListView list = new ListView();
    readonly Label hint = new Label();
    readonly Label hotkeyLabel = new Label();
    readonly TextBox hotkeyBox = new TextBox();
    string hotkeyText;   // the hotkey this window will save

    public SettingsForm(MenuHost host, List<Entry> entries)
    {
        this.host = host;
        work = entries;
        Text = "WinTools Custom Shortcuts - version " + MenuHost.Version;
        Icon = Dialogs.AppIcon(new Size(32, 32));
        Font = new Font("Segoe UI", 9.5f);
        StartPosition = FormStartPosition.CenterScreen;
        ClientSize = new Size(920, 540);
        KeyPreview = true;
        AllowDrop = true;
        DragEnter += OnDragEnter;
        DragDrop += OnDragDrop;

        hint.Text = "Drag rows or Alt+Up/Down to reorder. Untick to hide. Drop files to add. Double-click a letter to change it.";
        hint.Location = new Point(12, 10);
        hint.AutoSize = true;
        Controls.Add(hint);

        list.View = View.Details;
        list.FullRowSelect = true;
        list.CheckBoxes = true;
        list.HideSelection = false;
        list.Location = new Point(12, 36);
        list.Size = new Size(896, 400);
        list.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
        list.Columns.Add("Key", 50);
        list.Columns.Add("Name", 240);
        list.Columns.Add("Target", 400);
        list.Columns.Add("Type", 80);
        list.AllowDrop = true;
        list.DragEnter += OnDragEnter;
        list.DragDrop += OnDragDrop;
        list.DragOver += OnListDragOver;
        list.DragLeave += delegate { ClearDropHighlight(); };
        list.ItemDrag += delegate(object s, ItemDragEventArgs e)
        {
            ListViewItem it = e.Item as ListViewItem;
            if (it != null) list.DoDragDrop(new DragRow { Index = it.Index }, DragDropEffects.Move);
            ClearDropHighlight();
        };
        list.ItemChecked += delegate(object s, ItemCheckedEventArgs e) { ((Entry)e.Item.Tag).Hidden = !e.Item.Checked; };
        list.MouseDoubleClick += delegate(object s, MouseEventArgs e)
        {
            ListViewHitTestInfo hit = list.HitTest(e.Location);
            if (hit.Item != null && hit.Item.SubItems.IndexOf(hit.SubItem) == 0) ChangeLetter();
        };
        Controls.Add(list);

        AddButton("Up", 12, delegate { MoveSelected(-1); });
        AddButton("Down", 96, delegate { MoveSelected(1); });
        AddButton("Rename", 180, delegate { Rename(); });
        AddButton("Add files...", 264, delegate { AddWithDialog(); });
        AddButton("Remove custom", 372, delegate { RemoveCustom(); });
        AddButton("Reset to built-in", 500, delegate { ResetAll(); });
        AddButton("Change letter...", 628, delegate { ChangeLetter(); });
        AddButton("Cancel", 750, delegate { Close(); });
        AddButton("Save", 834, delegate { Save(); });

        Button steamBtn = new Button();
        steamBtn.Text = "Add Steam game...";
        steamBtn.Location = new Point(12, 492);
        steamBtn.Size = new Size(160, 30);
        steamBtn.Anchor = AnchorStyles.Bottom | AnchorStyles.Left;
        steamBtn.Click += delegate { AddSteam(); };
        Controls.Add(steamBtn);

        Button uninstallBtn = new Button();
        uninstallBtn.Text = "Uninstall program...";
        uninstallBtn.Location = new Point(748, 492);
        uninstallBtn.Size = new Size(160, 30);
        uninstallBtn.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;
        uninstallBtn.Click += delegate { MenuHost.UninstallProgram(); };
        Controls.Add(uninstallBtn);

        Button updateBtn = new Button();
        updateBtn.Text = "Check for updates...";
        updateBtn.Location = new Point(578, 492);
        updateBtn.Size = new Size(160, 30);
        updateBtn.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;
        updateBtn.Click += delegate { host.CheckNowIgnoringSkip(); };
        Controls.Add(updateBtn);

        Button githubBtn = new Button();
        githubBtn.Text = "View on GitHub";
        githubBtn.Location = new Point(746, 4);
        githubBtn.Size = new Size(162, 26);
        githubBtn.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        githubBtn.Click += delegate { MenuHost.OpenGitHub(); };
        Controls.Add(githubBtn);

        hotkeyText = Config.Hotkey;
        hotkeyLabel.Text = "Menu hotkey:";
        hotkeyLabel.AutoSize = true;
        hotkeyLabel.Location = new Point(190, 498);
        hotkeyLabel.Anchor = AnchorStyles.Bottom | AnchorStyles.Left;
        Controls.Add(hotkeyLabel);

        hotkeyBox.Text = hotkeyText;
        hotkeyBox.ReadOnly = true;
        hotkeyBox.Location = new Point(275, 494);
        hotkeyBox.Size = new Size(190, 24);
        hotkeyBox.Anchor = AnchorStyles.Bottom | AnchorStyles.Left;
        hotkeyBox.KeyDown += OnHotkeyKey;
        hotkeyBox.GotFocus += delegate { host.SuspendHotkey(); hotkeyBox.Text = "Press the new keys..."; };
        hotkeyBox.LostFocus += delegate { hotkeyBox.Text = hotkeyText; host.ResumeHotkey(); };
        Controls.Add(hotkeyBox);

        Button hotkeyDefault = new Button();
        hotkeyDefault.Text = "Default";
        hotkeyDefault.Location = new Point(475, 493);
        hotkeyDefault.Size = new Size(80, 26);
        hotkeyDefault.Anchor = AnchorStyles.Bottom | AnchorStyles.Left;
        hotkeyDefault.Click += delegate { hotkeyText = "Win+Alt+X"; hotkeyBox.Text = hotkeyText; };
        Controls.Add(hotkeyDefault);

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
        b.Size = new Size(text == "Remove custom" || text == "Reset to built-in" || text == "Change letter..." ? 116 : 80, 30);
        b.Anchor = AnchorStyles.Bottom | AnchorStyles.Left;
        b.Click += click;
        Controls.Add(b);
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.KeyCode == Keys.Escape) Close();
        // Alt+Up / Alt+Down move the selected entry, like the Up and Down buttons.
        if (e.Alt && (e.KeyCode == Keys.Up || e.KeyCode == Keys.Down) && !hotkeyBox.Focused)
        {
            MoveSelected(e.KeyCode == Keys.Up ? -1 : 1);
            e.Handled = true;
            e.SuppressKeyPress = true;
        }
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

    // Picks a new letter for the selected entry. If another entry has it, the two swap letters.
    void ChangeLetter()
    {
        int i = Selected();
        if (i < 0) return;
        string v = Dialogs.PickLetter(this, work[i].Title, work[i].Letter);
        if (v == null) return;
        if (v.Length > 0)
        {
            foreach (Entry other in work)
            {
                if (other != work[i] && string.Equals(other.Letter, v, StringComparison.OrdinalIgnoreCase)) other.Letter = work[i].Letter;
            }
        }
        work[i].Letter = v;
        Refresh(i);
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
        if (!host.ChangeHotkey(hotkeyText))
        {
            MessageBox.Show(this, hotkeyText + " cannot be used. Another program may be using it, or Windows reserves it. Pick another one.",
                "WinTools", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }
        Config.Hotkey = hotkeyText;
        Config.Save(work);
        host.Reload(work);
        Close();
    }

    [DllImport("user32.dll")] static extern short GetAsyncKeyState(int vKey);

    // Records the next key combination. Needs Win, Ctrl, Alt or Shift together with a key.
    void OnHotkeyKey(object sender, KeyEventArgs e)
    {
        // Escape and Tab keep their usual meaning (close, move on).
        if (e.KeyCode == Keys.Escape || e.KeyCode == Keys.Tab) return;
        e.Handled = true;
        e.SuppressKeyPress = true;
        Keys k = e.KeyCode;
        if (k == Keys.None || k == Keys.ControlKey || k == Keys.ShiftKey || k == Keys.Menu || k == Keys.LWin || k == Keys.RWin) return;
        bool win = (GetAsyncKeyState(0x5B) & 0x8000) != 0 || (GetAsyncKeyState(0x5C) & 0x8000) != 0;
        string combo = "";
        if (win) combo += "Win+";
        if (e.Control) combo += "Ctrl+";
        if (e.Alt) combo += "Alt+";
        if (e.Shift) combo += "Shift+";
        if (combo.Length == 0)
        {
            hotkeyBox.Text = "Hold Win, Ctrl, Alt or Shift too";
            return;
        }
        string keyName = k.ToString();
        if (keyName.Length == 2 && keyName[0] == 'D' && char.IsDigit(keyName[1])) keyName = keyName.Substring(1);
        hotkeyText = combo + keyName;
        hotkeyBox.Text = hotkeyText;
    }

    protected override void OnFormClosed(FormClosedEventArgs e)
    {
        host.ResumeHotkey();
        base.OnFormClosed(e);
    }

    // Dragging a row inside the list moves it; dropping files anywhere adds them.
    sealed class DragRow { public int Index; }

    void OnDragEnter(object sender, DragEventArgs e)
    {
        if (e.Data.GetDataPresent(typeof(DragRow))) e.Effect = DragDropEffects.Move;
        else e.Effect = e.Data.GetDataPresent(DataFormats.FileDrop) ? DragDropEffects.Copy : DragDropEffects.None;
    }

    // The row under the mouse while a row is dragged: highlighted, and scrolled into view near the edges.
    void OnListDragOver(object sender, DragEventArgs e)
    {
        OnDragEnter(sender, e);
        if (!e.Data.GetDataPresent(typeof(DragRow))) return;
        int target = DropIndex(e);
        for (int k = 0; k < list.Items.Count; k++)
        {
            Color c = k == target ? SystemColors.ControlLight : list.BackColor;
            if (list.Items[k].BackColor != c) list.Items[k].BackColor = c;
        }
        if (target >= 0) list.EnsureVisible(target);
    }

    void ClearDropHighlight()
    {
        foreach (ListViewItem it in list.Items) if (it.BackColor != list.BackColor) it.BackColor = list.BackColor;
    }

    // The row under the mouse, or the last row when below the rows.
    int DropIndex(DragEventArgs e)
    {
        Point p = list.PointToClient(new Point(e.X, e.Y));
        ListViewItem it = list.GetItemAt(8, p.Y);
        if (it != null) return it.Index;
        return list.Items.Count - 1;
    }

    void OnDragDrop(object sender, DragEventArgs e)
    {
        if (e.Data.GetDataPresent(typeof(DragRow)))
        {
            ClearDropHighlight();
            int from = ((DragRow)e.Data.GetData(typeof(DragRow))).Index;
            int to = DropIndex(e);
            if (from < 0 || from >= work.Count || to < 0 || to == from) return;
            Entry moved = work[from];
            work.RemoveAt(from);
            work.Insert(Math.Min(to, work.Count), moved);
            Refresh(work.IndexOf(moved));
            return;
        }
        string[] files = (string[])e.Data.GetData(DataFormats.FileDrop);
        if (files == null || files.Length == 0) return;
        foreach (string f in files) AddFile(f);
        Refresh(work.Count - 1);
    }
}
