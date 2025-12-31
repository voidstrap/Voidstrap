using System.ComponentModel;
using System.Windows.Forms;
using Voidstrap.UI.Elements.Bootstrapper.Base;

namespace Voidstrap.UI.Elements.Bootstrapper
{
    public partial class LegacyDialog2008 : WinFormsDialogBase
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
                    buttonCancel.Enabled = value;
            }
        }


        public LegacyDialog2008()
        {
            InitializeComponent(); // ✔ controls created first

            if (LicenseManager.UsageMode == LicenseUsageMode.Designtime)
                return;

            buttonCancel.Text = Strings.Common_Cancel;

            ScaleWindow();
            SetupDialog();

            ProgressBar.RightToLeft = RightToLeft;
            ProgressBar.RightToLeftLayout = RightToLeftLayout;
        }

        private void LegacyDialog2008_Load(object sender, System.EventArgs e)
        {
            if (!DesignMode)
                Activate();
        }
    }
}
