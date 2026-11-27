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

static class Dialogs
{
    // The program icon (embedded WinTools.ico) at the given size, or the standard one if it is missing.
    public static Icon AppIcon(Size size)
    {
        try
        {
            using (Stream st = typeof(Dialogs).Assembly.GetManifestResourceStream("WinTools.ico"))
            {
                if (st != null) return new Icon(st, size);
            }
        }
        catch (Exception) { }
        return SystemIcons.Application;
    }

    // Waits for one letter key for the entry. Returns "A".."Z", "" for no letter (Delete), or null if cancelled.
    public static string PickLetter(IWin32Window owner, string title, string current)
    {
        using (Form f = new Form())
        {
            string result = null;
            f.Text = "Change letter";
            f.FormBorderStyle = FormBorderStyle.FixedDialog;
            f.StartPosition = FormStartPosition.CenterParent;
            f.MaximizeBox = false;
            f.MinimizeBox = false;
            f.ShowInTaskbar = false;
            f.KeyPreview = true;
            f.ClientSize = new Size(380, 110);
            Label l = new Label();
            l.Text = "Press the new letter for \"" + title + "\" (now: " + (string.IsNullOrEmpty(current) ? "none" : current) + ").\n"
                + "If another item uses it, the two swap letters.\n"
                + "Delete = no letter. Esc = cancel.";
            l.Location = new Point(12, 12);
            l.Size = new Size(356, 60);
            f.Controls.Add(l);
            Button cancel = new Button();
            cancel.Text = "Cancel";
            cancel.DialogResult = DialogResult.Cancel;
            cancel.Location = new Point(288, 76);
            cancel.Width = 80;
            f.Controls.Add(cancel);
            f.CancelButton = cancel;
            f.KeyDown += delegate(object s, KeyEventArgs e)
            {
                if (e.Control || e.Alt) return;
                if (e.KeyCode >= Keys.A && e.KeyCode <= Keys.Z)
                {
                    result = ((char)('A' + (int)(e.KeyCode - Keys.A))).ToString();
                }
                else if (e.KeyCode == Keys.Delete || e.KeyCode == Keys.Back)
                {
                    result = "";
                }
                else return;
                e.SuppressKeyPress = true;
                f.DialogResult = DialogResult.OK;
            };
            return f.ShowDialog(owner) == DialogResult.OK ? result : null;
        }
    }

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
