using Voidstrap.UI.Elements.Bootstrapper.Base;
using Voidstrap.UI.ViewModels.Bootstrapper;
using System;
using System.ComponentModel;
using System.IO;
using System.Windows;
using System.Windows.Media.Imaging;
using System.Windows.Shell;
using System.Windows.Threading;
using System.Windows.Forms;

namespace Voidstrap.UI.Elements.Bootstrapper
{
    public partial class FluentDialog : IBootstrapperDialog
    {
        private readonly FluentDialogViewModel _viewModel;
        private bool _isClosing;

        public Voidstrap.Bootstrapper? Bootstrapper { get; set; }
        public string? CustomBackgroundPath { get; set; }

        #region Properties
        public string Message
        {
            get => _viewModel.Message;
            set => SetProperty(nameof(_viewModel.Message), value, v => _viewModel.Message = v);
        }

        public ProgressBarStyle ProgressStyle
        {
            get => _viewModel.ProgressIndeterminate ? ProgressBarStyle.Marquee : ProgressBarStyle.Continuous;
            set => SetProperty(nameof(_viewModel.ProgressIndeterminate), value == ProgressBarStyle.Marquee, v => _viewModel.ProgressIndeterminate = v);
        }

        public int ProgressMaximum
        {
            get => _viewModel.ProgressMaximum;
            set => SetProperty(nameof(_viewModel.ProgressMaximum), value, v => _viewModel.ProgressMaximum = v);
        }

        public int ProgressValue
        {
            get => _viewModel.ProgressValue;
            set => SetProperty(nameof(_viewModel.ProgressValue), value, v => _viewModel.ProgressValue = v);
        }

        public TaskbarItemProgressState TaskbarProgressState
        {
            get => _viewModel.TaskbarProgressState;
            set => SetProperty(nameof(_viewModel.TaskbarProgressState), value, v => _viewModel.TaskbarProgressState = v);
        }

        public double TaskbarProgressValue
        {
            get => _viewModel.TaskbarProgressValue;
            set => SetProperty(nameof(_viewModel.TaskbarProgressValue), value, v => _viewModel.TaskbarProgressValue = v);
        }

        public bool CancelEnabled
        {
            get => _viewModel.CancelEnabled;
            set
            {
                _viewModel.CancelEnabled = value;
                _viewModel.OnPropertyChanged(nameof(_viewModel.CancelEnabled));
                _viewModel.OnPropertyChanged(nameof(_viewModel.CancelButtonVisibility));
            }
        }
        #endregion

        public FluentDialog(bool aero)
        {
            InitializeComponent();
            _viewModel = new FluentDialogViewModel(this, aero);
            DataContext = _viewModel;

            Voidstrap.UI.Elements.Bootstrapper.AudioPlayerHelper.PlayStartupAudio();
            this.Closed += (s, e) =>
            {
                Voidstrap.UI.Elements.Bootstrapper.AudioPlayerHelper.StopAudio();
            };
            Title = App.Settings.Prop.BootstrapperTitle;
            Icon = App.Settings.Prop.BootstrapperIcon.GetIcon().GetImageSource();

            if (aero)
                AllowsTransparency = true;
            string? lastBackground = Directory.GetFiles(Paths.Base, "bootstrapper_bg.*").FirstOrDefault();
            if (lastBackground != null)
                CustomBackgroundPath = lastBackground;
            SetBackgroundImage();
            BackgroundEvents.BackgroundChanged += (path) =>
            {
                Dispatcher.Invoke(() => ChangeBackground(path));
            };
        }

        private void SetBackgroundImage()
        {
            BackgroundManager.SetBackgroundAsync(BackgroundImage, CustomBackgroundPath);

        }

        public async void ChangeBackground(string? newPath)
        {
            CustomBackgroundPath = newPath;
            await BackgroundManager.SetBackgroundAsync(BackgroundImage, CustomBackgroundPath);
        }

        private void UiWindow_Closing(object sender, CancelEventArgs e)
        {
            if (!_isClosing)
            {
                Bootstrapper?.Cancel();
                e.Cancel = true;
            }
        }

        #region IBootstrapperDialog Implementation
        public void ShowBootstrapper() => ShowDialog();

        public void CloseBootstrapper()
        {
            _isClosing = true;
            Dispatcher.InvokeAsync(Close, DispatcherPriority.Background);
        }

        public void ShowSuccess(string message, Action? callback = null) => BaseFunctions.ShowSuccess(message, callback);
        #endregion

        #region Helpers
        private void SetProperty<T>(string propertyName, T value, Action<T> setter)
        {
            setter(value);
            _viewModel.OnPropertyChanged(propertyName);
        }
        #endregion
    }
}
