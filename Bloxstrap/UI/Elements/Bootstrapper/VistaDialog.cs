using System;
using System.Windows.Forms;
using Voidstrap.UI.Elements.Bootstrapper.Base;

namespace Voidstrap.UI.Elements.Bootstrapper
{
    // https://youtu.be/h0_AL95Sc3o?t=48
    // Hidden WinForms host for TaskDialog

    public partial class VistaDialog : WinFormsDialogBase
    {
        private TaskDialogPage _dialogPage = null!;

        public sealed override string Message
        {
            get => _dialogPage?.Heading ?? string.Empty;
            set
            {
                if (_dialogPage != null)
                    _dialogPage.Heading = value;
            }
        }

        public sealed override ProgressBarStyle ProgressStyle
        {
            get => ProgressBarStyle.Continuous;
            set
            {
                if (_dialogPage?.ProgressBar is null)
                    return;

                _dialogPage.ProgressBar.State = value switch
                {
                    ProgressBarStyle.Continuous => TaskDialogProgressBarState.Normal,
                    ProgressBarStyle.Blocks => TaskDialogProgressBarState.Normal,
                    ProgressBarStyle.Marquee => TaskDialogProgressBarState.Marquee,
                    _ => _dialogPage.ProgressBar.State
                };
            }
        }

        public sealed override int ProgressMaximum
        {
            get => _dialogPage?.ProgressBar?.Maximum ?? 0;
            set
            {
                if (_dialogPage?.ProgressBar != null)
                    _dialogPage.ProgressBar.Maximum = value;
            }
        }

        public sealed override int ProgressValue
        {
            get => _dialogPage?.ProgressBar?.Value ?? 0;
            set
            {
                if (_dialogPage?.ProgressBar != null)
                    _dialogPage.ProgressBar.Value = value;
            }
        }

        public sealed override bool CancelEnabled
        {
            get => _dialogPage?.Buttons.Count > 0 && _dialogPage.Buttons[0].Enabled;
            set
            {
                if (_dialogPage?.Buttons.Count > 0)
                    _dialogPage.Buttons[0].Enabled = value;
            }
        }

        public VistaDialog()
        {
            InitializeComponent();

            _dialogPage = new TaskDialogPage
            {
                Icon = new TaskDialogIcon(
                    App.Settings.Prop.BootstrapperIcon.GetIcon()),
                Caption = App.Settings.Prop.BootstrapperTitle,
                RightToLeftLayout = Locale.RightToLeft,

                Buttons = { TaskDialogButton.Cancel },
                ProgressBar = new TaskDialogProgressBar
                {
                    State = TaskDialogProgressBarState.Marquee
                }
            };

            Message = "Please wait...";
            CancelEnabled = false;

            _dialogPage.Buttons[0].Click += ButtonCancel_Click;

            SetupDialog();
        }

        public override void ShowSuccess(string message, Action? callback)
        {
            if (InvokeRequired)
            {
                Invoke(ShowSuccess, message, callback);
                return;
            }

            TaskDialogPage successDialog = new()
            {
                Icon = TaskDialogIcon.ShieldSuccessGreenBar,
                Caption = App.Settings.Prop.BootstrapperTitle,
                Heading = message,
                Buttons = { TaskDialogButton.OK }
            };

            successDialog.Buttons[0].Click += (_, _) =>
            {
                callback?.Invoke();
                App.Terminate();
            };

            _dialogPage.Navigate(successDialog);
            _dialogPage = successDialog;
        }

        public override void CloseBootstrapper()
        {
            if (InvokeRequired)
            {
                Invoke(CloseBootstrapper);
                return;
            }

            _dialogPage.BoundDialog?.Close();
            base.CloseBootstrapper();
        }

        private void VistaDialog_Load(object sender, EventArgs e)
        {
            TaskDialog.ShowDialog(_dialogPage);
        }
    }
}
