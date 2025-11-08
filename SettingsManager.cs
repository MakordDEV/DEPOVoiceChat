using System.IO;
using UnityEngine;

namespace DEPOVoiceChat
{
    public enum MicMode
    {
        PushToTalk,
        VoiceActivation
    }

    [System.Serializable]
    public class VoiceSettings
    {
        public float selfVolume = 1f;
        public float playersVolume = 1f;
        public bool hearSelf = false;
        public int selectedMicIndex = 0;

        public KeyCode menuToggleKey = KeyCode.RightAlt;
        public KeyCode pushToTalkKey = KeyCode.R;

        public MicMode micMode = MicMode.PushToTalk;
        public float voiceThresholdDb = 5f;

        public string language = "English";
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

            Localization.SetLanguage(CurrentSettings.language);
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
