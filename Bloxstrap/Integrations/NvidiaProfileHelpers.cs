using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Voidstrap.Models;

namespace Voidstrap.Integrations
{
    internal static class NvidiaProfileHelpers
    {
        private static bool MatchesId(NvidiaEditorEntry entry, uint settingId)
        {
            if (entry.SettingId.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
            {
                return uint.TryParse(
                    entry.SettingId.Substring(2),
                    NumberStyles.HexNumber,
                    CultureInfo.InvariantCulture,
                    out var hex)
                    && hex == settingId;
            }

            return uint.TryParse(entry.SettingId, out var dec) && dec == settingId;
        }

        private static NvidiaEditorEntry Find(List<NvidiaEditorEntry> entries, uint settingId)
            => entries.FirstOrDefault(e => MatchesId(e, settingId));

        public static string GetEnum(
            List<NvidiaEditorEntry> entries,
            uint settingId,
            IList<string> uiValues,
            IList<int> driverValues,
            int defaultIndex)
        {
            var entry = Find(entries, settingId);
            if (entry == null)
                return uiValues[defaultIndex];

            if (!int.TryParse(entry.Value, out var raw))
                return uiValues[defaultIndex];

            int index = driverValues.IndexOf(raw);
            return index >= 0 ? uiValues[index] : uiValues[defaultIndex];
        }

        public static void SetEnum(
            List<NvidiaEditorEntry> entries,
            uint settingId,
            string selected,
            IList<string> uiValues,
            IList<int> driverValues)
        {
            int index = uiValues.IndexOf(selected);
            if (index < 0 || index >= driverValues.Count)
                return;

            SetValue(entries, settingId, driverValues[index].ToString());
        }

        public static bool GetBool(List<NvidiaEditorEntry> entries, uint settingId)
        {
            var entry = Find(entries, settingId);
            return entry != null && entry.Value == "1";
        }

        public static void SetBool(List<NvidiaEditorEntry> entries, uint settingId, bool value)
        {
            SetValue(entries, settingId, value ? "1" : "0");
        }

        private static void SetValue(
            List<NvidiaEditorEntry> entries,
            uint settingId,
            string value)
        {
            var entry = Find(entries, settingId);
            if (entry != null)
            {
                entry.Value = value;
                return;
            }

            entries.Add(new NvidiaEditorEntry
            {
                SettingId = $"0x{settingId:X}",
                Name = $"Setting 0x{settingId:X}",
                Value = value,
                ValueType = "Dword"
            });
        }
    }
}
