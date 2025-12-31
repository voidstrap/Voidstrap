using System.ComponentModel;
using System.Drawing;
using System.Windows.Forms;
using Voidstrap.UI.Elements.Bootstrapper.Base;

namespace Voidstrap.UI.Elements.Bootstrapper
{
    // basically just the modern dialog
    public partial class ProgressDialog : WinFormsDialogBase
    {
        public override string Message
        {
            get => labelMessage?.Text ?? string.Empty;
            set
            {
                if (labelMessage != null)
                    labelMessage.Text = value;
            }
        }

        public override ProgressBarStyle ProgressStyle
        {
            get => ProgressBar?.Style ?? ProgressBarStyle.Continuous;
            set
            {
                if (ProgressBar != null)
                    ProgressBar.Style = value;
            }
        }

        public override int ProgressMaximum
        {
            get => ProgressBar?.Maximum ?? 0;
            set
            {
                if (ProgressBar != null)
                    ProgressBar.Maximum = value;
            }
        }

        public override int ProgressValue
        {
            get => ProgressBar?.Value ?? 0;
            set
            {
                if (ProgressBar != null)
                    ProgressBar.Value = value;
            }
        }

        public override bool CancelEnabled
        {
            get => buttonCancel?.Enabled ?? false;
            set
            {
                if (buttonCancel != null)
                {
                    buttonCancel.Enabled = value;
                    buttonCancel.Visible = value;
                }
            }
        }

        public ProgressDialog()
        {
            InitializeComponent();

            if (LicenseManager.UsageMode == LicenseUsageMode.Designtime)
                return;

            if (App.Settings.Prop.Theme2.GetFinal() == Theme.Dark)
            {
                labelMessage.ForeColor = SystemColors.Window;
                buttonCancel.ForeColor = Color.FromArgb(196, 197, 196);
                buttonCancel.Image = Properties.Resources.DarkCancelButton;
                panel1.BackColor = Color.FromArgb(35, 37, 39);
                BackColor = Color.FromArgb(25, 27, 29);
            }

            labelMessage.Text = Strings.Bootstrapper_StylePreview_TextCancel;
            buttonCancel.Text = Strings.Common_Cancel;

            IconBox.BackgroundImage =
                App.Settings.Prop.BootstrapperIcon
                    .GetIcon()
                    .GetSized(128, 128)
                    .ToBitmap();

            SetupDialog();

            ProgressBar.RightToLeft = RightToLeft;
            ProgressBar.RightToLeftLayout = RightToLeftLayout;
        }

        private void ButtonCancel_MouseEnter(object sender, System.EventArgs e)
        {
            if (App.Settings.Prop.Theme2.GetFinal() == Theme.Dark)
                buttonCancel.Image = Properties.Resources.DarkCancelButtonHover;
            else
                buttonCancel.Image = Properties.Resources.CancelButtonHover;
        }

        private void ButtonCancel_MouseLeave(object sender, System.EventArgs e)
        {
            if (App.Settings.Prop.Theme2.GetFinal() == Theme.Dark)
                buttonCancel.Image = Properties.Resources.DarkCancelButton;
            else
                buttonCancel.Image = Properties.Resources.CancelButton;
        }

        private void ProgressDialog_Load(object sender, System.EventArgs e)
        {
            if (!DesignMode)
                Activate();
        }
    }
}
