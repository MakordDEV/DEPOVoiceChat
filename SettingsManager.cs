using UnityEngine;
using System.IO;

namespace DEPOVoiceChat
{
    [System.Serializable]
    public class VoiceSettings
    {
        public float selfVolume = 1f;
        public float playersVolume = 1f;
        public bool hearSelf = false;
        public int selectedMicIndex = 0;
    }

    public static class SettingsManager
    {
        private static string settingsPath = Path.Combine(Application.persistentDataPath, "voicechat_settings.json");

        public static VoiceSettings CurrentSettings { get; private set; } = new VoiceSettings();

        public static void Load()
        {
            if (File.Exists(settingsPath))
            {
                try
                {
                    string json = File.ReadAllText(settingsPath);
                    CurrentSettings = JsonUtility.FromJson<VoiceSettings>(json);
                }
                catch { CurrentSettings = new VoiceSettings(); }
            }
        }

        public static void Save()
        {
            try
            {
                string json = JsonUtility.ToJson(CurrentSettings, true);
                File.WriteAllText(settingsPath, json);
            }
            catch { }
        }
    }
}
