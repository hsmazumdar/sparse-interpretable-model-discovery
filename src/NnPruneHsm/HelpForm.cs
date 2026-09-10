using System;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

namespace VnnBp
{
    /// <summary>
    /// Read-only reviewer guide loaded from Help.rtf beside the executable.
    /// Keeping the instructions in an external RTF lets reviewers inspect,
    /// copy and print the experimental procedure without changing the code.
    /// </summary>
    public sealed class HelpForm : Form
    {
        private readonly RichTextBox helpText;
        private readonly Button closeButton;

        public HelpForm()
        {
            Text = "NnPruneHsm - Reviewer Help";
            StartPosition = FormStartPosition.CenterParent;
            MinimumSize = new Size(700, 500);
            ClientSize = new Size(900, 650);

            helpText = new RichTextBox();
            helpText.Dock = DockStyle.Fill;
            helpText.ReadOnly = true;
            helpText.BackColor = SystemColors.Window;
            helpText.DetectUrls = true;
            helpText.HideSelection = false;

            closeButton = new Button();
            closeButton.Text = "Close";
            closeButton.Dock = DockStyle.Bottom;
            closeButton.Height = 32;
            closeButton.Click += delegate { Close(); };

            Controls.Add(helpText);
            Controls.Add(closeButton);
            AcceptButton = closeButton;
            CancelButton = closeButton;

            Load += HelpForm_Load;
        }

        private void HelpForm_Load(object sender, EventArgs e)
        {
            string helpPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Help.rtf");
            if (!File.Exists(helpPath))
            {
                helpText.Text = "Help.rtf was not found. Expected location:\r\n" + helpPath;
                return;
            }

            try
            {
                helpText.LoadFile(helpPath, RichTextBoxStreamType.RichText);
            }
            catch (ArgumentException)
            {
                // Remain usable if a reviewer replaces Help.rtf with plain text.
                helpText.Text = File.ReadAllText(helpPath);
            }
            catch (IOException ex)
            {
                helpText.Text = "Help.rtf could not be opened:\r\n" + ex.Message;
            }
        }
    }
}
