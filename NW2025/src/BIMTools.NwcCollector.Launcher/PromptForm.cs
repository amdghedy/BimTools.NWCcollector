using System.Drawing;
using System.Windows.Forms;

namespace BIMTools.NwcCollector.Launcher
{
    /// <summary>Tiny "enter one line of text" dialog.</summary>
    internal static class PromptForm
    {
        public static string Show(IWin32Window owner, string title, string label, string defaultText)
        {
            using (var form = new Form())
            {
                form.Text = title;
                form.FormBorderStyle = FormBorderStyle.FixedDialog;
                form.StartPosition = FormStartPosition.CenterParent;
                form.MinimizeBox = false;
                form.MaximizeBox = false;
                form.ClientSize = new Size(420, 110);

                var lbl = new Label { Text = label, AutoSize = true, Location = new Point(12, 12) };
                var box = new TextBox { Text = defaultText ?? "", Location = new Point(12, 36), Width = 396 };
                var ok = new Button { Text = "OK", DialogResult = DialogResult.OK, Location = new Point(252, 72), Width = 75 };
                var cancel = new Button { Text = "Cancel", DialogResult = DialogResult.Cancel, Location = new Point(333, 72), Width = 75 };
                form.Controls.AddRange(new Control[] { lbl, box, ok, cancel });
                form.AcceptButton = ok;
                form.CancelButton = cancel;

                return form.ShowDialog(owner) == DialogResult.OK && box.Text.Trim().Length > 0 ? box.Text.Trim() : null;
            }
        }
    }
}
