using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using CommunityToolkit.Mvvm.Input;
using Voidstrap.Integrations.SwiftTunnel;
using Voidstrap.Integrations.SwiftTunnel.Models;
using Voidstrap.UI.Elements.Dialogs;

namespace Voidstrap.UI.ViewModels.Settings
{
    public class SwiftTunnelViewModel : NotifyPropertyChangedViewModel, IDisposable
    {
        private readonly SwiftTunnelService _service;
        private string _email = "";
        private string _password = "";
        private string _errorMessage = "";
        private bool _isBusy;
        private bool _disposed;

        public SwiftTunnelViewModel()
        {
            _service = SwiftTunnelService.Instance;

            // Subscribe to events
            _service.AuthStateChanged += OnAuthStateChanged;
            _service.ConnectionStateChanged += OnConnectionStateChanged;

            // Initialize async
            _ = InitializeAsync();
        }

        private async Task InitializeAsync()
        {
            await _service.InitializeAsync();
            RefreshAllProperties();
        }

        #region Authentication Properties

        public string Email
        {
            get => _email;
            set
            {
                _email = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(CanSignIn));
            }
        }

        public string Password
        {
            get => _password;
            set
            {
                _password = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(CanSignIn));
            }
        }

        public string ErrorMessage
        {
            get => _errorMessage;
            set
            {
                _errorMessage = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(HasError));
            }
        }

        public bool HasError => !string.IsNullOrEmpty(ErrorMessage);

        public bool IsBusy
        {
            get => _isBusy;
            set
            {
                _isBusy = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(IsNotBusy));
                OnPropertyChanged(nameof(CanSignIn));
                OnPropertyChanged(nameof(CanToggleConnection));
            }
        }

        public bool IsNotBusy => !IsBusy;

        public bool CanSignIn => !IsBusy && !string.IsNullOrWhiteSpace(Email) && !string.IsNullOrWhiteSpace(Password);

        public bool IsAuthenticated => _service.IsAuthenticated;

        public bool IsNotAuthenticated => !IsAuthenticated;

        public string? UserEmail => _service.AuthManager.UserEmail;

        public string? UserDisplayName => _service.AuthManager.UserDisplayName ?? UserEmail;

        #endregion

        #region Connection Properties

        public bool IsConnected => _service.IsConnected;

        public ConnectionState CurrentState => _service.ConnectionState;

        public string ConnectionStatusText => CurrentState switch
        {
            ConnectionState.Disconnected => "Disconnected",
            ConnectionState.FetchingConfig => "Fetching configuration...",
            ConnectionState.CreatingAdapter => "Creating adapter...",
            ConnectionState.Connecting => "Connecting...",
            ConnectionState.ConfiguringSplitTunnel => "Configuring split tunnel...",
            ConnectionState.Connected => "Connected",
            ConnectionState.Disconnecting => "Disconnecting...",
            ConnectionState.Error => "Error",
            _ => "Unknown"
        };

        public string ConnectionDetails
        {
            get
            {
                if (!IsConnected) return "";
                var region = GamingRegions.GetByRegion(SelectedRegion);
                return region != null ? $"{region.DisplayName} ({region.CountryCode})" : SelectedRegion;
            }
        }

        public Brush ConnectionStatusColor => CurrentState switch
        {
            ConnectionState.Connected => new SolidColorBrush(Color.FromRgb(76, 175, 80)), // Green
            ConnectionState.Error => new SolidColorBrush(Color.FromRgb(244, 67, 54)), // Red
            ConnectionState.Disconnected => new SolidColorBrush(Color.FromRgb(158, 158, 158)), // Gray
            _ => new SolidColorBrush(Color.FromRgb(255, 193, 7)) // Yellow (connecting)
        };

        public string ConnectButtonText => IsConnected ? "Disconnect" : "Connect";

        public string ConnectButtonAppearance => IsConnected ? "Danger" : "Primary";

        public bool CanToggleConnection => !IsBusy && IsAuthenticated && !IsConnecting;

        private bool IsConnecting => CurrentState switch
        {
            ConnectionState.FetchingConfig => true,
            ConnectionState.CreatingAdapter => true,
            ConnectionState.Connecting => true,
            ConnectionState.ConfiguringSplitTunnel => true,
            ConnectionState.Disconnecting => true,
            _ => false
        };

        #endregion

        #region Settings Properties

        public bool IsEnabled
        {
            get => App.Settings.Prop.SwiftTunnelEnabled;
            set
            {
                App.Settings.Prop.SwiftTunnelEnabled = value;
                OnPropertyChanged();
            }
        }

        public string SelectedRegion
        {
            get => App.Settings.Prop.SwiftTunnelRegion;
            set
            {
                App.Settings.Prop.SwiftTunnelRegion = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(ConnectionDetails));
            }
        }

        public bool AutoConnect
        {
            get => App.Settings.Prop.SwiftTunnelAutoConnect;
            set
            {
                App.Settings.Prop.SwiftTunnelAutoConnect = value;
                OnPropertyChanged();
            }
        }

        public bool SplitTunnel
        {
            get => App.Settings.Prop.SwiftTunnelSplitTunnel;
            set
            {
                App.Settings.Prop.SwiftTunnelSplitTunnel = value;
                OnPropertyChanged();
            }
        }

        public bool RememberLogin
        {
            get => App.Settings.Prop.SwiftTunnelRememberLogin;
            set
            {
                App.Settings.Prop.SwiftTunnelRememberLogin = value;
                OnPropertyChanged();
            }
        }

        public Dictionary<string, string> Regions { get; } = new()
        {
            ["singapore"] = "Singapore",
            ["mumbai"] = "Mumbai",
            ["sydney"] = "Sydney",
            ["tokyo"] = "Tokyo",
            ["germany"] = "Germany",
            ["paris"] = "Paris",
            ["america"] = "America",
            ["brazil"] = "Brazil"
        };

        #endregion

        #region Commands

        public ICommand SignInCommand => new AsyncRelayCommand(SignInAsync);
        public ICommand SignInWithGoogleCommand => new AsyncRelayCommand(SignInWithGoogleAsync);
        public ICommand SignOutCommand => new AsyncRelayCommand(SignOutAsync);
        public ICommand ToggleConnectionCommand => new AsyncRelayCommand(ToggleConnectionAsync);

        private async Task SignInAsync()
        {
            if (string.IsNullOrWhiteSpace(Email) || string.IsNullOrWhiteSpace(Password))
                return;

            IsBusy = true;
            ErrorMessage = "";

            try
            {
                var (success, error) = await _service.AuthManager.SignInAsync(Email, Password);

                if (!success)
                {
                    ErrorMessage = error ?? "Sign in failed";
                }
                else
                {
                    // Clear form on success
                    Email = "";
                    Password = "";
                }
            }
            catch (Exception ex)
            {
                ErrorMessage = ex.Message;
            }
            finally
            {
                IsBusy = false;
            }
        }

        private async Task SignInWithGoogleAsync()
        {
            IsBusy = true;
            ErrorMessage = "";

            try
            {
                var dialog = new SwiftTunnelOAuthDialog();
                var (accessToken, refreshToken) = await dialog.ShowAndWaitAsync();

                if (!string.IsNullOrEmpty(accessToken))
                {
                    var (success, error) = await _service.AuthManager.HandleOAuthCallbackAsync(accessToken, refreshToken ?? "");

                    if (!success)
                    {
                        ErrorMessage = error ?? "OAuth sign in failed";
                    }
                }
            }
            catch (Exception ex)
            {
                ErrorMessage = ex.Message;
            }
            finally
            {
                IsBusy = false;
            }
        }

        private async Task SignOutAsync()
        {
            IsBusy = true;

            try
            {
                // Disconnect VPN first if connected
                if (IsConnected)
                {
                    await _service.DisconnectAsync();
                }

                await _service.AuthManager.SignOutAsync();
            }
            catch (Exception ex)
            {
                App.Logger.WriteLine("SwiftTunnelViewModel", $"Sign out error: {ex.Message}");
            }
            finally
            {
                IsBusy = false;
            }
        }

        private async Task ToggleConnectionAsync()
        {
            IsBusy = true;
            ErrorMessage = "";

            try
            {
                if (IsConnected)
                {
                    var (_, error) = await _service.DisconnectAsync();
                    if (error != null)
                    {
                        ErrorMessage = error;
                    }
                }
                else
                {
                    var (_, error) = await _service.ConnectAsync(SelectedRegion);
                    if (error != null)
                    {
                        ErrorMessage = error;
                    }
                }
            }
            catch (Exception ex)
            {
                ErrorMessage = ex.Message;
            }
            finally
            {
                IsBusy = false;
            }
        }

        #endregion

        #region Event Handlers

        private void OnAuthStateChanged(object? sender, bool isAuthenticated)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                OnPropertyChanged(nameof(IsAuthenticated));
                OnPropertyChanged(nameof(IsNotAuthenticated));
                OnPropertyChanged(nameof(UserEmail));
                OnPropertyChanged(nameof(UserDisplayName));
                OnPropertyChanged(nameof(CanToggleConnection));
            });
        }

        private void OnConnectionStateChanged(object? sender, ConnectionState state)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                OnPropertyChanged(nameof(IsConnected));
                OnPropertyChanged(nameof(CurrentState));
                OnPropertyChanged(nameof(ConnectionStatusText));
                OnPropertyChanged(nameof(ConnectionDetails));
                OnPropertyChanged(nameof(ConnectionStatusColor));
                OnPropertyChanged(nameof(ConnectButtonText));
                OnPropertyChanged(nameof(ConnectButtonAppearance));
                OnPropertyChanged(nameof(CanToggleConnection));
            });
        }

        private void RefreshAllProperties()
        {
            OnPropertyChanged(nameof(IsAuthenticated));
            OnPropertyChanged(nameof(IsNotAuthenticated));
            OnPropertyChanged(nameof(UserEmail));
            OnPropertyChanged(nameof(UserDisplayName));
            OnPropertyChanged(nameof(IsConnected));
            OnPropertyChanged(nameof(CurrentState));
            OnPropertyChanged(nameof(ConnectionStatusText));
            OnPropertyChanged(nameof(ConnectionDetails));
            OnPropertyChanged(nameof(ConnectionStatusColor));
            OnPropertyChanged(nameof(ConnectButtonText));
            OnPropertyChanged(nameof(ConnectButtonAppearance));
            OnPropertyChanged(nameof(CanToggleConnection));
            OnPropertyChanged(nameof(IsEnabled));
            OnPropertyChanged(nameof(SelectedRegion));
            OnPropertyChanged(nameof(AutoConnect));
            OnPropertyChanged(nameof(SplitTunnel));
            OnPropertyChanged(nameof(RememberLogin));
        }

        #endregion

        public void Dispose()
        {
            if (_disposed)
                return;

            _disposed = true;

            _service.AuthStateChanged -= OnAuthStateChanged;
            _service.ConnectionStateChanged -= OnConnectionStateChanged;
        }
    }
}
