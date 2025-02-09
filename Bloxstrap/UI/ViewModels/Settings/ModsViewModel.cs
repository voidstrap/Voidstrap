using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using Microsoft.Win32;
using Windows.Win32;
using Windows.Win32.UI.Shell;
using Windows.Win32.Foundation;
using CommunityToolkit.Mvvm.Input;
using Hellstrap.Models.SettingTasks;
using Hellstrap.AppData;
using Hellstrap.Enums.FlagPresets;

namespace Hellstrap.UI.ViewModels.Settings
{
    public class ModsViewModel : NotifyPropertyChangedViewModel
    {
        private static readonly Dictionary<string, byte[]> FontHeaders = new()
        {
            { "ttf", new byte[] { 0x00, 0x01, 0x00, 0x00 } },
            { "otf", new byte[] { 0x4F, 0x54, 0x54, 0x4F } },
            { "ttc", new byte[] { 0x74, 0x74, 0x63, 0x66 } }
        };

        public ICommand OpenModsFolderCommand => new RelayCommand(() => Process.Start("explorer.exe", Paths.Mods));
        public ICommand ManageCustomFontCommand => new RelayCommand(ManageCustomFont);
        public ICommand OpenCompatSettingsCommand => new RelayCommand(OpenCompatSettings);

        public Visibility ChooseCustomFontVisibility =>
            string.IsNullOrEmpty(TextFontTask.NewState) ? Visibility.Visible : Visibility.Collapsed;

        public Visibility DeleteCustomFontVisibility =>
            string.IsNullOrEmpty(TextFontTask.NewState) ? Visibility.Collapsed : Visibility.Visible;

        public ModPresetTask OldDeathSoundTask { get; } = new("OldDeathSound", "content\\sounds\\ouch.ogg", "Sounds.OldDeath.ogg");
        public ModPresetTask OldAvatarBackgroundTask { get; } = new("OldAvatarBackground", "ExtraContent\\places\\Mobile.rbxl", "OldAvatarBackground.rbxl");
        public FontModPresetTask TextFontTask { get; } = new();
        public EmojiModPresetTask EmojiFontTask { get; } = new();

        public ModPresetTask OldCharacterSoundsTask { get; } = new("OldCharacterSounds", new()
        {
            { "content\\sounds\\action_footsteps_plastic.mp3", "Sounds.OldWalk.mp3" },
            { "content\\sounds\\action_jump.mp3", "Sounds.OldJump.mp3" },
            { "content\\sounds\\action_get_up.mp3", "Sounds.OldGetUp.mp3" },
            { "content\\sounds\\action_falling.mp3", "Sounds.Empty.mp3" },
            { "content\\sounds\\action_jump_land.mp3", "Sounds.Empty.mp3" },
            { "content\\sounds\\action_swim.mp3", "Sounds.Empty.mp3" },
            { "content\\sounds\\impact_water.mp3", "Sounds.Empty.mp3" }
        });

        public EnumModPresetTask<Enums.CursorType> CursorTypeTask { get; } = new("CursorType", new()
        {
            { Enums.CursorType.DotCursor, new() {
                { "content\\textures\\Cursors\\KeyboardMouse\\ArrowCursor.png", "Cursor.DotCursor.ArrowCursor.png" },
                { "content\\textures\\Cursors\\KeyboardMouse\\ArrowFarCursor.png", "Cursor.DotCursor.ArrowFarCursor.png" }
            }},
            { Enums.CursorType.StoofsCursor, new() {
                { "content\\textures\\Cursors\\KeyboardMouse\\ArrowCursor.png", "Cursor.StoofsCursor.ArrowCursor.png" },
                { "content\\textures\\Cursors\\KeyboardMouse\\ArrowFarCursor.png", "Cursor.StoofsCursor.ArrowFarCursor.png" }
            }},
            { Enums.CursorType.CleanCursor, new() {
                { "content\\textures\\Cursors\\KeyboardMouse\\ArrowCursor.png", "Cursor.CleanCursor.ArrowCursor.png" },
                { "content\\textures\\Cursors\\KeyboardMouse\\ArrowFarCursor.png", "Cursor.CleanCursor.ArrowFarCursor.png" }
            }},
            { Enums.CursorType.FPSCursor, new() {
                { "content\\textures\\Cursors\\KeyboardMouse\\ArrowCursor.png", "Cursor.FPSCursor.ArrowCursor.png" },
                { "content\\textures\\Cursors\\KeyboardMouse\\ArrowFarCursor.png", "Cursor.FPSCursor.ArrowFarCursor.png" }
            }},
            { Enums.CursorType.From2006, new() {
                { "content\\textures\\Cursors\\KeyboardMouse\\ArrowCursor.png", "Cursor.From2006.ArrowCursor.png" },
                { "content\\textures\\Cursors\\KeyboardMouse\\ArrowFarCursor.png", "Cursor.From2006.ArrowFarCursor.png" }
            }},
            { Enums.CursorType.From2013, new() {
                { "content\\textures\\Cursors\\KeyboardMouse\\ArrowCursor.png", "Cursor.From2013.ArrowCursor.png" },
                { "content\\textures\\Cursors\\KeyboardMouse\\ArrowFarCursor.png", "Cursor.From2013.ArrowFarCursor.png" }
            }}
        });

        private void ManageCustomFont()
        {
            if (!string.IsNullOrEmpty(TextFontTask.NewState))
            {
                TextFontTask.NewState = string.Empty;
            }
            else
            {
                var dialog = new OpenFileDialog { Filter = $"{Strings.Menu_FontFiles}|*.ttf;*.otf;*.ttc" };

                if (dialog.ShowDialog() != true) return;

                string type = Path.GetExtension(dialog.FileName).TrimStart('.').ToLowerInvariant();
                byte[] fileHeader = File.ReadAllBytes(dialog.FileName).Take(4).ToArray();

                if (!FontHeaders.TryGetValue(type, out var expectedHeader) || !expectedHeader.SequenceEqual(fileHeader))
                {
                    Frontend.ShowMessageBox(Strings.Menu_Mods_Misc_CustomFont_Invalid, MessageBoxImage.Error);
                    return;
                }

                TextFontTask.NewState = dialog.FileName;
            }

            OnPropertyChanged(nameof(ChooseCustomFontVisibility));
            OnPropertyChanged(nameof(DeleteCustomFontVisibility));
        }

        private void OpenCompatSettings()
        {
            string path = new RobloxPlayerData().ExecutablePath;

            if (File.Exists(path))
                PInvoke.SHObjectProperties(HWND.Null, SHOP_TYPE.SHOP_FILEPATH, path, "Compatibility");
            else
                Frontend.ShowMessageBox(Strings.Common_RobloxNotInstalled, MessageBoxImage.Error);
        }
    }
}
