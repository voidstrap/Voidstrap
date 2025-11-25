using Microsoft.Win32;
using System;
using System.Collections.Generic;

namespace Voidstrap.Utility
{
    internal static class WindowsRegistry
    {
        private const string RobloxPlaceKey = "Roblox.Place";
        private const string ClassesRoot = @"Software\Classes\";

        public static readonly List<RegistryKey> Roots = new()
        {
            Registry.CurrentUser,
            Registry.LocalMachine
        };
        public static void RegisterProtocol(string key, string name, string handler, string handlerParam = "%1")
        {
            string handlerArgs = $"\"{handler}\" {handlerParam}";
            string protocolKeyPath = $"{ClassesRoot}{key}";

            try
            {
                using var uriKey = Registry.CurrentUser.CreateSubKey(protocolKeyPath);
                using var uriIconKey = uriKey?.CreateSubKey("DefaultIcon");
                using var uriCommandKey = uriKey?.CreateSubKey(@"shell\\open\\command");

                if (uriKey == null || uriIconKey == null || uriCommandKey == null)
                    throw new InvalidOperationException($"Failed to create subkeys for {key}");

                uriKey.SetValueSafe("", $"URL:{name} Protocol");
                uriKey.SetValueSafe("URL Protocol", "");

                uriIconKey.SetValueSafe("", handler);
                uriCommandKey.SetValueSafe("", handlerArgs);
            }
            catch (Exception ex)
            {
                App.Logger.WriteLine("Registry::RegisterProtocol", $"Failed to register {key}: {ex}");
            }
        }
        public static void RegisterPlayer() => RegisterPlayer(Paths.Application, "-player \"%1\"");

        public static void RegisterPlayer(string handler, string handlerParam)
        {
            RegisterProtocol("roblox", "Roblox", handler, handlerParam);
            RegisterProtocol("roblox-player", "Roblox", handler, handlerParam);
        }
        public static void RegisterStudio()
        {
            RegisterStudioProtocol(Paths.Application, "-studio \"%1\"");
            RegisterStudioFileClass(Paths.Application, "-studio \"%1\"");
            RegisterStudioFileTypes();
        }
        public static void RegisterStudioProtocol(string handler, string handlerParam)
        {
            RegisterProtocol("roblox-studio", "Roblox Studio", handler, handlerParam);
            RegisterProtocol("roblox-studio-auth", "Roblox Studio Auth", handler, handlerParam);
        }
        public static void RegisterStudioFileTypes()
        {
            RegisterStudioFileType(".rbxl");
            RegisterStudioFileType(".rbxlx");
        }
        public static void RegisterStudioFileClass(string handler, string handlerParam)
        {
            const string classDisplayName = "Roblox Place";
            string handlerArgs = $"\"{handler}\" {handlerParam}";
            string iconValue = $"{handler},0";

            try
            {
                using var uriKey = Registry.CurrentUser.CreateSubKey($"{ClassesRoot}{RobloxPlaceKey}");
                using var uriIconKey = uriKey?.CreateSubKey("DefaultIcon");
                using var uriOpenKey = uriKey?.CreateSubKey(@"shell\\open");
                using var uriCommandKey = uriOpenKey?.CreateSubKey("command");

                if (uriKey == null || uriIconKey == null || uriCommandKey == null)
                    throw new InvalidOperationException($"Failed to create subkeys for {RobloxPlaceKey}");

                uriKey.SetValueSafe("", classDisplayName);
                uriOpenKey.SetValueSafe("", "Open");
                uriIconKey.SetValueSafe("", iconValue);
                uriCommandKey.SetValueSafe("", handlerArgs);
            }
            catch (Exception ex)
            {
                App.Logger.WriteLine("Registry::RegisterStudioFileClass", $"Failed: {ex}");
            }
        }
        public static void RegisterStudioFileType(string extension)
        {
            try
            {
                using var uriKey = Registry.CurrentUser.CreateSubKey($"{ClassesRoot}{extension}");
                uriKey?.CreateSubKey($"{RobloxPlaceKey}\\ShellNew");

                if (uriKey != null)
                    uriKey.SetValueSafe("", RobloxPlaceKey);
            }
            catch (Exception ex)
            {
                App.Logger.WriteLine("Registry::RegisterStudioFileType", $"Failed for {extension}: {ex}");
            }
        }
        public static void RegisterApis()
        {
            try
            {
                using var apisKey = Registry.CurrentUser.CreateSubKey(App.ApisKey);
                apisKey?.SetValueSafe("ApplicationPath", Paths.Application);
                apisKey?.SetValueSafe("InstallationPath", Paths.Base);
            }
            catch (Exception ex)
            {
                App.Logger.WriteLine("Registry::RegisterApis", $"Failed: {ex}");
            }
        }
        public static void Unregister(string key)
        {
            try
            {
                Registry.CurrentUser.DeleteSubKeyTree($"{ClassesRoot}{key}", throwOnMissingSubKey: false);
            }
            catch (Exception ex)
            {
                App.Logger.WriteLine("Registry::Unregister", $"Failed to unregister {key}: {ex}");
            }
        }
    }
}
