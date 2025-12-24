using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Data;
using Voidstrap.UI.ViewModels.Settings;

namespace Voidstrap.UI.Elements.Settings.Pages
{
    public partial class BehaviourPage
    {
        public BehaviourPage()
        {
            InitializeComponent();
            DataContext = new BehaviourViewModel();
            LoadLuaScript();
        }

        private void LoadLuaScript()
        {
            try
            {
                string luaScriptPath = Path.Combine(Paths.Base, "autoexecute.lua");
                if (File.Exists(luaScriptPath))
                {
                    string luaScript = File.ReadAllText(luaScriptPath);
                    LuaScriptEditor.Text = luaScript;
                }
                else
                {
                    // Set default example script
                    LuaScriptEditor.Text = "-- Lua Script Example\n-- This script executes when Roblox launches\n\n-- Call the example function\nlocal result = ExampleFunction()\nprint(\"ExampleFunction returned: \" .. tostring(result))";
                }
            }
            catch (Exception ex)
            {
                App.Logger?.WriteLine("ModsPage::LoadLuaScript", $"Error loading Lua script: {ex.Message}");
            }
        }

        private void SaveLuaScript_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                string luaScriptPath = Path.Combine(Paths.Base, "autoexecute.lua");
                string luaScript = LuaScriptEditor.Text;

                File.WriteAllText(luaScriptPath, luaScript);

                App.Logger?.WriteLine("ModsPage::SaveLuaScript", $"Lua script saved to {luaScriptPath}");
            }
            catch (Exception ex)
            {
                App.Logger?.WriteLine("ModsPage::SaveLuaScript", $"Error saving Lua script: {ex.Message}");
                Frontend.ShowMessageBox($"Error saving Lua script: {ex.Message}", MessageBoxImage.Error);
            }
        }

        public void SaveCurrentLuaScript()
        {
            SaveLuaScript_Click(null!, null!);
        }
    }
}
