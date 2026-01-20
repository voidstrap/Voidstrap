using System.Web;
using System.Windows;
using Microsoft.Web.WebView2.Core;

namespace Voidstrap.UI.Elements.Dialogs
{
    /// <summary>
    /// OAuth dialog for SwiftTunnel Google sign-in
    /// </summary>
    public partial class SwiftTunnelOAuthDialog
    {
        private const string AuthBaseUrl = "https://auth.swifttunnel.net";
        private const string RedirectUrl = "https://swifttunnel.net/auth/callback";

        private readonly TaskCompletionSource<(string? AccessToken, string? RefreshToken)> _completionSource;
        private bool _isCompleted;

        public SwiftTunnelOAuthDialog()
        {
            InitializeComponent();
            _completionSource = new TaskCompletionSource<(string?, string?)>();
            Loaded += OnLoaded;
            Closing += OnClosing;
        }

        /// <summary>
        /// Show the dialog and wait for authentication result
        /// </summary>
        public Task<(string? AccessToken, string? RefreshToken)> ShowAndWaitAsync()
        {
            Show();
            return _completionSource.Task;
        }

        private async void OnLoaded(object sender, RoutedEventArgs e)
        {
            try
            {
                LoadingText.Text = "Initializing browser...";

                // Initialize WebView2
                var env = await CoreWebView2Environment.CreateAsync();
                await OAuthWebView.EnsureCoreWebView2Async(env);

                // Clear cookies for clean login
                OAuthWebView.CoreWebView2.CookieManager.DeleteAllCookies();

                // Navigate to OAuth URL
                LoadingText.Text = "Connecting to SwiftTunnel...";
                var oauthUrl = $"{AuthBaseUrl}/auth/v1/authorize?provider=google&redirect_to={Uri.EscapeDataString(RedirectUrl)}";
                OAuthWebView.Source = new Uri(oauthUrl);

                App.Logger.WriteLine("SwiftTunnelOAuthDialog", $"Navigating to OAuth URL: {oauthUrl}");
            }
            catch (Exception ex)
            {
                App.Logger.WriteLine("SwiftTunnelOAuthDialog", $"Failed to initialize WebView2: {ex.Message}");
                CompleteWithError("Failed to initialize browser. Please try again.");
            }
        }

        private void OnClosing(object? sender, System.ComponentModel.CancelEventArgs e)
        {
            if (!_isCompleted)
            {
                _completionSource.TrySetResult((null, null));
            }
        }

        private void OAuthWebView_NavigationStarting(object sender, CoreWebView2NavigationStartingEventArgs e)
        {
            App.Logger.WriteLine("SwiftTunnelOAuthDialog", $"Navigation starting: {e.Uri}");

            // Check if this is the callback URL
            if (e.Uri.StartsWith(RedirectUrl))
            {
                e.Cancel = true;
                HandleCallback(e.Uri);
            }
        }

        private void OAuthWebView_NavigationCompleted(object sender, CoreWebView2NavigationCompletedEventArgs e)
        {
            // Hide loading overlay when page loads
            LoadingOverlay.Visibility = Visibility.Collapsed;
            OAuthWebView.Visibility = Visibility.Visible;

            if (!e.IsSuccess)
            {
                App.Logger.WriteLine("SwiftTunnelOAuthDialog", $"Navigation failed: {e.WebErrorStatus}");
            }
        }

        private void HandleCallback(string url)
        {
            try
            {
                App.Logger.WriteLine("SwiftTunnelOAuthDialog", "Handling OAuth callback...");

                // Parse the URL - tokens might be in hash fragment or query string
                var uri = new Uri(url);

                string? accessToken = null;
                string? refreshToken = null;

                // Check hash fragment first (Supabase uses hash for implicit flow)
                if (!string.IsNullOrEmpty(uri.Fragment) && uri.Fragment.Length > 1)
                {
                    var fragment = uri.Fragment.Substring(1); // Remove leading #
                    var fragmentParams = HttpUtility.ParseQueryString(fragment);
                    accessToken = fragmentParams["access_token"];
                    refreshToken = fragmentParams["refresh_token"];
                }

                // Also check query string
                if (string.IsNullOrEmpty(accessToken) && !string.IsNullOrEmpty(uri.Query))
                {
                    var queryParams = HttpUtility.ParseQueryString(uri.Query);
                    accessToken = queryParams["access_token"];
                    refreshToken = queryParams["refresh_token"];

                    // Check for error
                    var error = queryParams["error"];
                    var errorDescription = queryParams["error_description"];
                    if (!string.IsNullOrEmpty(error))
                    {
                        App.Logger.WriteLine("SwiftTunnelOAuthDialog", $"OAuth error: {error} - {errorDescription}");
                        CompleteWithError(errorDescription ?? error);
                        return;
                    }
                }

                if (!string.IsNullOrEmpty(accessToken))
                {
                    App.Logger.WriteLine("SwiftTunnelOAuthDialog", "OAuth tokens received successfully");
                    CompleteWithTokens(accessToken, refreshToken);
                }
                else
                {
                    App.Logger.WriteLine("SwiftTunnelOAuthDialog", "No tokens found in callback URL");
                    CompleteWithError("Authentication failed. Please try again.");
                }
            }
            catch (Exception ex)
            {
                App.Logger.WriteLine("SwiftTunnelOAuthDialog", $"Error handling callback: {ex.Message}");
                CompleteWithError("Failed to process authentication response.");
            }
        }

        private void CompleteWithTokens(string accessToken, string? refreshToken)
        {
            _isCompleted = true;
            _completionSource.TrySetResult((accessToken, refreshToken));
            Dispatcher.Invoke(() => Close());
        }

        private void CompleteWithError(string message)
        {
            _isCompleted = true;
            _completionSource.TrySetResult((null, null));

            Dispatcher.Invoke(() =>
            {
                MessageBox.Show(message, "Authentication Failed", MessageBoxButton.OK, MessageBoxImage.Error);
                Close();
            });
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            _isCompleted = true;
            _completionSource.TrySetResult((null, null));
            Close();
        }
    }
}
