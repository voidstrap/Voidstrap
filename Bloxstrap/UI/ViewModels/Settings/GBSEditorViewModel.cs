using System;
using System.IO;
using System.Linq;
using System.Windows.Input;
using System.Xml.Linq;
using Voidstrap.UI.ViewModels;
using Voidstrap.UI.ViewModels.ContextMenu;

namespace Voidstrap.UI.ViewModels.Settings
{
    public class GBSEditorViewModel : NotifyPropertyChangedViewModel
    {
        private readonly string _settingsPath =
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "Roblox", "GlobalBasicSettings_13.xml");

        private XDocument? _doc;
        private XElement? _props;
        public ICommand ResetToDefaultsCommand { get; }

        public GBSEditorViewModel()
        {
            ResetToDefaultsCommand = new RelayCommand(ResetToDefaults);
            LoadSettings();
        }

        private void LoadSettings()
        {
            if (!File.Exists(_settingsPath))
                return;

            _doc = XDocument.Load(_settingsPath);
            _props = _doc.Descendants("Properties").FirstOrDefault();
        }

        private void SaveSettings()
        {
            try
            {
                _doc?.Save(_settingsPath);
            }
            catch { }
        }

        private string GetValue(string name, string defaultValue)
        {
            if (_props == null) return defaultValue;

            var element = _props.Elements().FirstOrDefault(e => e.Attribute("name")?.Value == name);
            if (element == null)
            {
                element = new XElement("string", defaultValue);
                element.SetAttributeValue("name", name);
                _props.Add(element);
                SaveSettings();
                return defaultValue;
            }

            return element.Value;
        }

        private void SetValue(string name, string value, string? type = null)
        {
            if (_props == null) return;

            var element = _props.Elements().FirstOrDefault(e => e.Attribute("name")?.Value == name);
            if (element == null)
            {
                element = new XElement(type ?? "string", value);
                element.SetAttributeValue("name", name);
                _props.Add(element);
            }
            else
            {
                element.Value = value;
            }

            SaveSettings();
        }

        private bool GetBool(string name, bool defaultValue = false)
        {
            var val = GetValue(name, defaultValue ? "true" : "false");
            return val.Equals("true", StringComparison.OrdinalIgnoreCase);
        }

        private void SetBool(string name, bool value)
        {
            SetValue(name, value.ToString().ToLower(), "bool");
        }

        private int GetInt(string name, int defaultValue)
        {
            var val = GetValue(name, defaultValue.ToString());
            return int.TryParse(val, out var result) ? result : defaultValue;
        }

        private void SetInt(string name, int value)
        {
            SetValue(name, value.ToString(), "int");
        }

        private float GetFloat(string name, float defaultValue)
        {
            var val = GetValue(name, defaultValue.ToString("G", System.Globalization.CultureInfo.InvariantCulture));
            return float.TryParse(val, System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out var result)
                ? result : defaultValue;
        }

        private void SetFloat(string name, float value)
        {
            SetValue(name, value.ToString("G", System.Globalization.CultureInfo.InvariantCulture), "float");
        }

        public float UITransparency
        {
            get => GetFloat("PreferredTransparency", 1f);
            set { SetFloat("PreferredTransparency", value); OnPropertyChanged(); }
        }

        public int PreferredTextSize
        {
            get => GetInt("PreferredTextSize", 1);
            set { SetInt("PreferredTextSize", value); OnPropertyChanged(); }
        }

        public bool ReducedMotion
        {
            get => GetBool("ReducedMotion", true);
            set { SetBool("ReducedMotion", value); OnPropertyChanged(); }
        }

        public bool HudVisible
        {
            get => GetBool("UsedHideHudShortcut", false) == false;
            set { SetBool("UsedHideHudShortcut", !value); OnPropertyChanged(); }
        }

        public int FramerateCap
        {
            get => GetInt("FramerateCap", 60);
            set { SetInt("FramerateCap", value); OnPropertyChanged(); }
        }

        public bool VignetteEnabled
        {
            get => GetBool("VignetteEnabled", true);
            set { SetBool("VignetteEnabled", value); OnPropertyChanged(); }
        }

        public int GraphicsQuality
        {
            get => GetInt("SavedQualityLevel", 10);
            set { SetInt("SavedQualityLevel", value); OnPropertyChanged(); }
        }

        public bool Fullscreen
        {
            get => GetBool("Fullscreen", true);
            set { SetBool("Fullscreen", value); OnPropertyChanged(); }
        }

        public bool VSyncEnabled
        {
            get => GetBool("VSyncEnabled", false);
            set { SetBool("VSyncEnabled", value); OnPropertyChanged(); }
        }

        public float MasterVolume
        {
            get => GetFloat("MasterVolume", 1f);
            set { SetFloat("MasterVolume", value); OnPropertyChanged(); }
        }

        public float VoiceChatVolume
        {
            get => GetFloat("PartyVoiceVolume", 1f);
            set { SetFloat("PartyVoiceVolume", value); OnPropertyChanged(); }
        }

        public float PartyVoiceVolume
        {
            get => GetFloat("PartyVoiceVolume", 1f);
            set { SetFloat("PartyVoiceVolume", value); OnPropertyChanged(); }
        }

        public float MouseSensitivity
        {
            get => GetFloat("MouseSensitivity", 1f);
            set { SetFloat("MouseSensitivity", value); OnPropertyChanged(); }
        }

        public bool CameraYInverted
        {
            get => GetBool("CameraYInverted", false);
            set { SetBool("CameraYInverted", value); OnPropertyChanged(); }
        }

        public float GamepadSensitivity
        {
            get => GetFloat("GamepadCameraSensitivity", 0.2f);
            set { SetFloat("GamepadCameraSensitivity", value); OnPropertyChanged(); }
        }

        public bool ControllerVibration
        {
            get => GetBool("HapticStrength", true);
            set { SetBool("HapticStrength", value); OnPropertyChanged(); }
        }

        public bool VREnabled
        {
            get => GetBool("VREnabled", false);
            set { SetBool("VREnabled", value); OnPropertyChanged(); }
        }

        public int VRComfortSetting
        {
            get => GetInt("VRComfortSetting", 2);
            set { SetInt("VRComfortSetting", value); OnPropertyChanged(); }
        }

        public bool NetworkStatsVisible
        {
            get => GetBool("PerformanceStatsVisible", false);
            set { SetBool("PerformanceStatsVisible", value); OnPropertyChanged(); }
        }

        public bool ChatTranslationEnabled
        {
            get => GetBool("ChatTranslationEnabled", true);
            set { SetBool("ChatTranslationEnabled", value); OnPropertyChanged(); }
        }

        public bool MicroProfilerWebServerEnabled
        {
            get => GetBool("MicroProfilerWebServerEnabled", false);
            set { SetBool("MicroProfilerWebServerEnabled", value); OnPropertyChanged(); }
        }

        public bool OnScreenProfilerEnabled
        {
            get => GetBool("OnScreenProfilerEnabled", false);
            set { SetBool("OnScreenProfilerEnabled", value); OnPropertyChanged(); }
        }

        public bool PerformanceStatsVisible
        {
            get => GetBool("PerformanceStatsVisible", false);
            set { SetBool("PerformanceStatsVisible", value); OnPropertyChanged(); }
        }

        public bool PlayerNamesEnabled
        {
            get => GetBool("PlayerNamesEnabled", true);
            set { SetBool("PlayerNamesEnabled", value); OnPropertyChanged(); }
        }

        public bool BadgeVisible
        {
            get => GetBool("BadgeVisible", true);
            set { SetBool("BadgeVisible", value); OnPropertyChanged(); }
        }

        public bool ChatVisible
        {
            get => GetBool("ChatVisible", true);
            set { SetBool("ChatVisible", value); OnPropertyChanged(); }
        }

        public void ResetToDefaults()
        {
            try
            {
                if (File.Exists(_settingsPath))
                {
                    File.Delete(_settingsPath);
                }
                _doc = new XDocument(
                    new XElement("roblox",
                        new XElement("Item",
                            new XAttribute("class", "UserGameSettings"),
                            new XElement("Properties")
                        )
                    )
                );

                _props = _doc.Descendants("Properties").FirstOrDefault();
                SaveSettings();
                OnPropertyChanged(string.Empty);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to reset settings: {ex}");
            }
        }
    }
}
