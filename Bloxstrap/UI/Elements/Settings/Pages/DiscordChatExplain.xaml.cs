using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Threading.Tasks;
using System.Windows.Controls;
using Microsoft.Win32;

namespace Voidstrap.UI.Elements.Settings.Pages
{
    public partial class DiscordChatExplain
    {
        public DiscordChatExplain()
        {
            InitializeComponent();
        }

        private void Button_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            NavigationService.Navigate(new IntegrationsPage());
        }
    }
}