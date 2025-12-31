using System.ComponentModel;
using System.Windows.Forms;
using Voidstrap.UI.Elements.Bootstrapper.Base;

namespace Voidstrap.UI.Elements.Bootstrapper
{
    // https://youtu.be/3K9oCEMHj2s?t=35
    public partial class LegacyDialog2011 : WinFormsDialogBase
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

        public LegacyDialog2011()
        {
            InitializeComponent();

            if (LicenseManager.UsageMode == LicenseUsageMode.Designtime)
                return;

            IconBox.BackgroundImage =
                App.Settings.Prop.BootstrapperIcon.GetIcon().ToBitmap();

            buttonCancel.Text = Strings.Common_Cancel;

            ScaleWindow();
            SetupDialog();

            ProgressBar.RightToLeft = RightToLeft;
            ProgressBar.RightToLeftLayout = RightToLeftLayout;
        }

        private void LegacyDialog2011_Load(object sender, System.EventArgs e)
        {
            if (!DesignMode)
                Activate();
        }
    }
}
