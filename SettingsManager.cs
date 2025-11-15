using System.IO;
using UnityEngine;

namespace DEPOVoiceChat
{
    /// <summary>
    /// possible microphone modes for voice chat, either push-to-talk or voice activation
    /// </summary>
    public enum MicMode
    {
        PushToTalk,
        VoiceActivation
    }

    /// <summary>
    /// stores all user settings for voice chat, including volumes, keybinds, mic mode, language, and buffer size
    /// </summary>
    [System.Serializable]
    public class VoiceSettings
    {
        // volume levels
        public float selfVolume = 1f;
        public float playersVolume = 1f;
        public bool hearSelf = false;
        public int selectedMicIndex = 0;

        // keybindings for menu and push-to-talk
        public KeyCode menuToggleKey = KeyCode.RightAlt;
        public KeyCode pushToTalkKey = KeyCode.R;

        // microphone mode and voice threshold
        public MicMode micMode = MicMode.PushToTalk;
        public float voiceThresholdDb = 5f;

        // interface language
        public string language = "English";

        // audio buffer in milliseconds
        public int bufferSizeMs = 20;
    }

    /// <summary>
    /// handles loading and saving voice chat settings to persistent storage
    /// </summary>
    public static class SettingsManager
    {
        // full path to the settings file in persistent storage
        private static string settingsPath = Path.Combine(Application.persistentDataPath, "voicechat_settings.json");

        // current loaded settings
        public static VoiceSettings CurrentSettings { get; private set; } = new VoiceSettings();

        /// <summary>
        /// loads settings from disk, falls back to defaults if file is missing or corrupted
        /// sets the localization language after loading
        /// </summary>
        public static void Load()
        {
            if (File.Exists(settingsPath))
            {
                try
                {
                    string json = File.ReadAllText(settingsPath);
                    CurrentSettings = JsonUtility.FromJson<VoiceSettings>(json);
                }
                catch
                {
                    // reset to default settings if reading fails
                    CurrentSettings = new VoiceSettings();
                }
            }

            Localization.SetLanguage(CurrentSettings.language);
        }

        /// <summary>
        /// saves the current settings to disk
        /// fails silently if writing fails
        /// </summary>
        public static void Save()
        {
            try
            {
                string json = JsonUtility.ToJson(CurrentSettings, true);
                File.WriteAllText(settingsPath, json);
            }
            catch
            {
                // ignore any errors during save
            }
        }
    }
}
