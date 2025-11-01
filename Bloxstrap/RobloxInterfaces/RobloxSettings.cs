using System;
using System.IO;
using System.Xml.Linq;

namespace Voidstrap
{
    public static class RobloxSettings
    {
        private static readonly string SettingsPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Roblox",
            "GlobalBasicSettings_13.xml"
        );

        public static bool IsUncapped()
        {
            try
            {
                if (!File.Exists(SettingsPath)) return false;

                var doc = XDocument.Load(SettingsPath);
                var fpsElement = FindFPSElement(doc);

                return fpsElement != null && int.TryParse(fpsElement.Value, out int fps) && fps > 240;
            }
            catch
            {
                return false;
            }
        }

        public static void SetUncapped(bool uncap)
        {
            try
            {
                if (!File.Exists(SettingsPath))
                {
                    var newDoc = new XDocument(
                        new XElement("robloxSettings",
                            new XElement("int", new XAttribute("name", "FramerateCap"), uncap ? "9999" : "-1")
                        )
                    );
                    Directory.CreateDirectory(Path.GetDirectoryName(SettingsPath)!);
                    newDoc.Save(SettingsPath);
                    return;
                }

                var doc = XDocument.Load(SettingsPath);
                var fpsElement = FindFPSElement(doc);

                if (fpsElement != null)
                    fpsElement.Value = uncap ? "9999" : "-1";
                else
                    doc.Root?.Add(new XElement("int", new XAttribute("name", "FramerateCap"), uncap ? "9999" : "-1"));

                doc.Save(SettingsPath);
            }
            catch
            {
            }
        }

        private static XElement? FindFPSElement(XDocument doc)
        {
            foreach (var intElement in doc.Descendants("int"))
            {
                var nameAttr = intElement.Attribute("name");
                if (nameAttr != null && nameAttr.Value == "FramerateCap")
                    return intElement;
            }
            return null;
        }

        public static bool IsChatVisible()
        {
            try
            {
                if (!File.Exists(SettingsPath)) return true;

                var doc = XDocument.Load(SettingsPath);
                var chatElement = FindChatElement(doc);

                if (chatElement == null) return true;

                return chatElement.Value.ToLower() == "true";
            }
            catch
            {
                return true;
            }
        }

        public static void SetChatVisible(bool visible)
        {
            try
            {
                if (!File.Exists(SettingsPath))
                {
                    var newDoc = new XDocument(
                        new XElement("robloxSettings",
                            new XElement("bool", new XAttribute("name", "ChatVisible"), visible ? "true" : "false")
                        )
                    );
                    Directory.CreateDirectory(Path.GetDirectoryName(SettingsPath)!);
                    newDoc.Save(SettingsPath);
                    return;
                }

                var doc = XDocument.Load(SettingsPath);
                var chatElement = FindChatElement(doc);

                if (chatElement != null)
                    chatElement.Value = visible ? "true" : "false";
                else
                    doc.Root?.Add(new XElement("bool", new XAttribute("name", "ChatVisible"), visible ? "true" : "false"));

                doc.Save(SettingsPath);
            }
            catch
            {
            }
        }

        private static XElement? FindChatElement(XDocument doc)
        {
            foreach (var boolElement in doc.Descendants("bool"))
            {
                var nameAttr = boolElement.Attribute("name");
                if (nameAttr != null && nameAttr.Value == "ChatVisible")
                    return boolElement;
            }
            return null;
        }
    }
}
