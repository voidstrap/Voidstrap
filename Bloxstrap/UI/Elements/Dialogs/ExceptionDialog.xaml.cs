using System;
using System.Media;
using System.Web;
using System.Windows;
using System.Windows.Interop;

using Windows.Win32;
using Windows.Win32.Foundation;

namespace Voidstrap.UI.Elements.Dialogs
{
    // this entire code is so stupid but hada clean it up
    public partial class ExceptionDialog
    {
        private static readonly int MaxGitHubUrlLength = 8192;
        private static readonly int MaxLogLength = 7000;

        public ExceptionDialog(Exception exception)
        {
            InitializeComponent();
            AddExceptionToTextBox(exception);

            if (!App.Logger.Initialized)
                LocateLogFileButton.Content = Strings.Dialog_Exception_CopyLogContents;

            string repoUrl = $"https://github.com/{App.ProjectRepository}";
            string wikiUrl = $"https://voidstrapp.netlify.app/documentation/documentation";

            string title = HttpUtility.UrlEncode($"[BUG] {exception.GetType()}: {exception.Message}");
            string log = HttpUtility.UrlEncode(
                App.Logger.AsDocument.Length > MaxLogLength
                    ? App.Logger.AsDocument.Substring(0, MaxLogLength)
                    : App.Logger.AsDocument
            );

            string issueUrl = $"{repoUrl}/issues/new?template=bug_report.yaml&title={title}&log={log}";

            if (issueUrl.Length > MaxGitHubUrlLength)
            {
                issueUrl = $"{repoUrl}/issues/new?template=bug_report.yaml&title={title}";

                if (issueUrl.Length > MaxGitHubUrlLength)
                    issueUrl = $"{repoUrl}/issues/new?template=bug_report.yaml";
            }

            HelpMessageMDTextBlock.MarkdownText = GetHelpMessage(wikiUrl, issueUrl);
            VersionText.Text = string.Format(Strings.Dialog_Exception_Version, App.Version);
            ReportExceptionButton.Click += (_, _) => Utilities.ShellExecute(issueUrl);
            LocateLogFileButton.Click += OnLocateLogFileClick;
            CloseButton.Click += (_, _) => Close();

            SystemSounds.Hand.Play();
            Loaded += (_, _) => FlashWindowOnLoad();
        }

        #region Helpers

        private void AddExceptionToTextBox(Exception exception)
        {
            void AppendException(Exception ex, bool isInner)
            {
                if (ex == null) return;

                if (!isInner)
                    ErrorRichTextBox.Selection.Text = $"{ex.GetType()}: {ex.Message}";
                else
                    ErrorRichTextBox.Selection.Text += $"\n\n[Inner Exception]\n{ex.GetType()}: {ex.Message}";

                AppendException(ex.InnerException, true);
            }

            AppendException(exception, false);
        }

        private string GetHelpMessage(string wikiUrl, string issueUrl) // when I read this 'gethelp' I just feel like I need help 💀
        {
            if (!App.IsActionBuild &&
                !App.BuildMetadata.Machine.Contains("pizzaboxer", StringComparison.Ordinal)) // ah yes we use pizzabox
            {
                return string.Format(Strings.Dialog_Exception_Info_2_Alt, wikiUrl);
            }

            return string.Format(Strings.Dialog_Exception_Info_2, wikiUrl, issueUrl);
        }

        private void OnLocateLogFileClick(object sender, RoutedEventArgs e)
        {
            if (App.Logger.Initialized && !string.IsNullOrEmpty(App.Logger.FileLocation))
                Utilities.ShellExecute(App.Logger.FileLocation);
            else
                Clipboard.SetDataObject(App.Logger.AsDocument);
        }

        private void FlashWindowOnLoad()
        {
            IntPtr hWnd = new WindowInteropHelper(this).Handle;
            PInvoke.FlashWindow((HWND)hWnd, true);
        }

        #endregion
    }
}