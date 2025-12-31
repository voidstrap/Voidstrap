using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Windows.Forms;
using System.Windows.Shell;
using Voidstrap.UI.Utility;

namespace Voidstrap.UI.Elements.Bootstrapper.Base
{
    public class WinFormsDialogBase : Form, IBootstrapperDialog
    {
        public const int TaskbarProgressMaximum = 100;

        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public Voidstrap.Bootstrapper? Bootstrapper { get; set; }

        private bool _isClosing;

        #region UI State (BACKING FIELDS)

        protected string _message = "Please wait...";
        protected ProgressBarStyle _progressStyle;
        protected int _progressValue;
        protected int _progressMaximum;
        protected TaskbarItemProgressState _taskbarProgressState;
        protected double _taskbarProgressValue;
        protected bool _cancelEnabled;

        #endregion

        #region OVERRIDABLE PROPERTIES  🔥🔥🔥

        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public virtual string Message
        {
            get => _message;
            set
            {
                if (InvokeRequired)
                    Invoke(new Action(() => _message = value));
                else
                    _message = value;
            }
        }

        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public virtual ProgressBarStyle ProgressStyle
        {
            get => _progressStyle;
            set
            {
                if (InvokeRequired)
                    Invoke(new Action(() => _progressStyle = value));
                else
                    _progressStyle = value;
            }
        }

        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public virtual int ProgressMaximum
        {
            get => _progressMaximum;
            set
            {
                if (InvokeRequired)
                    Invoke(new Action(() => _progressMaximum = value));
                else
                    _progressMaximum = value;
            }
        }

        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public virtual int ProgressValue
        {
            get => _progressValue;
            set
            {
                if (InvokeRequired)
                    Invoke(new Action(() => _progressValue = value));
                else
                    _progressValue = value;
            }
        }

        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public virtual bool CancelEnabled
        {
            get => _cancelEnabled;
            set
            {
                if (InvokeRequired)
                    Invoke(new Action(() => _cancelEnabled = value));
                else
                    _cancelEnabled = value;
            }
        }

        #endregion

        #region TASKBAR (unchanged)

        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public TaskbarItemProgressState TaskbarProgressState
        {
            get => _taskbarProgressState;
            set
            {
                _taskbarProgressState = value;
                TaskbarProgress.SetProgressState(
                    Process.GetCurrentProcess().MainWindowHandle,
                    value);
            }
        }

        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public double TaskbarProgressValue
        {
            get => _taskbarProgressValue;
            set
            {
                _taskbarProgressValue = value;
                TaskbarProgress.SetProgressValue(
                    Process.GetCurrentProcess().MainWindowHandle,
                    (int)value,
                    TaskbarProgressMaximum);
            }
        }

        #endregion

        #region WINDOW SETUP

        public void ScaleWindow()
        {
            Size = MinimumSize = MaximumSize = WindowScaling.GetScaledSize(Size);

            foreach (Control control in Controls)
            {
                control.Size = WindowScaling.GetScaledSize(control.Size);
                control.Location = WindowScaling.GetScaledPoint(control.Location);
                control.Padding = WindowScaling.GetScaledPadding(control.Padding);
            }
        }

        public void SetupDialog()
        {
            if (LicenseManager.UsageMode == LicenseUsageMode.Designtime)
                return;

            Text = App.Settings.Prop.BootstrapperTitle;
            Icon = App.Settings.Prop.BootstrapperIcon.GetIcon();

            if (Locale.RightToLeft)
            {
                RightToLeft = RightToLeft.Yes;
                RightToLeftLayout = true;
            }
        }

        #endregion

        #region EVENTS

        public void ButtonCancel_Click(object? sender, EventArgs e) => Close();

        public void Dialog_FormClosing(object sender, FormClosingEventArgs e)
        {
            if (!_isClosing)
                Bootstrapper?.Cancel();
        }

        #endregion

        #region IBootstrapperDialog

        public void ShowBootstrapper() => ShowDialog();

        public virtual void CloseBootstrapper()
        {
            if (InvokeRequired)
                Invoke(new Action(CloseBootstrapper));
            else
            {
                _isClosing = true;
                Close();
            }
        }

        public virtual void ShowSuccess(string message, Action? callback)
            => BaseFunctions.ShowSuccess(message, callback);

        #endregion
    }
}
