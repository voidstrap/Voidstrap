using Voidstrap.Integrations;
using Voidstrap.UI.Elements.About;
using Voidstrap.UI.Elements.ContextMenu;
using System;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Windows.Threading;

namespace Voidstrap.UI
{
    public class NotifyIconWrapper : IDisposable
    {
        private bool _disposed;

        private readonly NotifyIcon _notifyIcon;
        private readonly MenuContainer _menuContainer;
        private readonly Watcher _watcher;

        private ActivityWatcher? ActivityWatcher => _watcher.ActivityWatcher;

        private EventHandler? _alertClickHandler;

        public NotifyIconWrapper(Watcher watcher)
        {
            App.Logger.WriteLine("NotifyIconWrapper::NotifyIconWrapper", "Initializing notification area icon");

            _watcher = watcher ?? throw new ArgumentNullException(nameof(watcher));

            _notifyIcon = new NotifyIcon(new System.ComponentModel.Container())
            {
                Icon = Properties.Resources.IconVoidstrap,
                Text = "Voidstrap",
                Visible = true
            };
            _notifyIcon.MouseClick += NotifyIcon_MouseClick;

            if (ActivityWatcher != null && App.Settings.Prop.ShowServerDetails)
                ActivityWatcher.OnGameJoin += async (s, e) => await OnGameJoinAsync(s, e);

            _menuContainer = new MenuContainer(_watcher);
            _menuContainer.Show();
        }

        private void NotifyIcon_MouseClick(object? sender, MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Right)
                return;

            _menuContainer.Dispatcher.Invoke(() =>
            {
                _menuContainer.Activate();
                _menuContainer.ContextMenu.IsOpen = true;
            });
        }

        public async Task OnGameJoinAsync(object? sender, EventArgs e)
        {
            if (ActivityWatcher == null)
                return;

            string? serverLocation = await ActivityWatcher.Data.QueryServerLocation();

            if (string.IsNullOrEmpty(serverLocation))
                return;

            string title = ActivityWatcher.Data.ServerType switch
            {
                ServerType.Public => Strings.ContextMenu_ServerInformation_Notification_Title_Public,
                ServerType.Private => Strings.ContextMenu_ServerInformation_Notification_Title_Private,
                ServerType.Reserved => Strings.ContextMenu_ServerInformation_Notification_Title_Reserved,
                _ => string.Empty
            };

            ShowAlert(
                title,
                string.Format(Strings.ContextMenu_ServerInformation_Notification_Text, serverLocation),
                10,
                (_, _) => _menuContainer.Dispatcher.Invoke(() => _menuContainer.ShowServerInformationWindow())
            );
        }

        public void ShowAlert(string caption, string message, int durationSeconds, EventHandler? clickHandler)
        {
            string id = Guid.NewGuid().ToString("N").Substring(0, 8);
            string logIdent = $"NotifyIconWrapper::ShowAlert.{id}";

            App.Logger.WriteLine(logIdent, $"Showing alert for {durationSeconds}s (clickHandler set: {clickHandler != null})");
            App.Logger.WriteLine(logIdent, $"{caption}: {message.Replace("\n", "\\n")}");

            if (_alertClickHandler != null)
            {
                App.Logger.WriteLine(logIdent, "Previous alert present, removing old click handler");
                _notifyIcon.BalloonTipClicked -= _alertClickHandler;
                _alertClickHandler = null;
            }

            _notifyIcon.BalloonTipTitle = caption;
            _notifyIcon.BalloonTipText = message;

            if (clickHandler != null)
            {
                _alertClickHandler = clickHandler;
                _notifyIcon.BalloonTipClicked += _alertClickHandler;
            }

            _notifyIcon.ShowBalloonTip(durationSeconds);

            _ = Task.Run(async () =>
            {
                await Task.Delay(durationSeconds * 1000);

                if (clickHandler != null)
                {
                    _notifyIcon.BalloonTipClicked -= clickHandler;

                    App.Logger.WriteLine(logIdent, "Alert duration ended, removed click handler");

                    if (_alertClickHandler == clickHandler)
                    {
                        _alertClickHandler = null;
                    }
                    else
                    {
                        App.Logger.WriteLine(logIdent, "Click handler was overridden by another alert");
                    }
                }
            });
        }

        public void Dispose()
        {
            if (_disposed)
                return;

            _disposed = true;

            App.Logger.WriteLine("NotifyIconWrapper::Dispose", "Disposing NotifyIcon");

            try
            {
                _menuContainer.Dispatcher.Invoke(() => _menuContainer.Close());
            }
            catch (Exception ex)
            {
                App.Logger.WriteLine("NotifyIconWrapper::Dispose", $"Failed close menu container: {ex}");
            }

            if (_alertClickHandler != null)
            {
                _notifyIcon.BalloonTipClicked -= _alertClickHandler;
                _alertClickHandler = null;
            }

            _notifyIcon.Dispose();

            GC.SuppressFinalize(this);
        }
    }
}
