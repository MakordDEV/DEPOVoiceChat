using System.Collections.Generic;

namespace DEPOVoiceChat
{
    public static class Localization
    {
        public enum Language { English, Russian }

        public static Language CurrentLanguage { get; private set; } = Language.English;

        private static Dictionary<string, Dictionary<Language, string>> texts = new Dictionary<string, Dictionary<Language, string>>()
        {
            { "connected_clients", new Dictionary<Language, string> { { Language.English, "Connected clients:" }, { Language.Russian, "Подключено клиентов:" } } },
            { "volume_players", new Dictionary<Language, string> { { Language.English, "Volume players" }, { Language.Russian, "Громкость игроков" } } },
            { "volume_microphone", new Dictionary<Language, string> { { Language.English, "Volume microphone" }, { Language.Russian, "Громкость микрофона" } } },
            { "hear_myself", new Dictionary<Language, string> { { Language.English, "Hear myself" }, { Language.Russian, "Слушать себя" } } },
            { "select_microphone", new Dictionary<Language, string> { { Language.English, "Select microphone" }, { Language.Russian, "Выбрать микрофон" } } },
            { "keybinds", new Dictionary<Language, string> { { Language.English, "Keybinds:" }, { Language.Russian, "Назначение клавиш:" } } },
            { "open_close_menu", new Dictionary<Language, string> { { Language.English, "Open/Close menu" }, { Language.Russian, "Открыть/Закрыть меню" } } },
            { "push_to_talk", new Dictionary<Language, string> { { Language.English, "Push-to-talk" }, { Language.Russian, "Нажать для речи" } } },
            { "close", new Dictionary<Language, string> { { Language.English, "Close" }, { Language.Russian, "Закрыть" } } },
            { "microphone_mode", new Dictionary<Language, string> { { Language.English, "Microphone mode:" }, { Language.Russian, "Режим микрофона:" } } },
            { "threshold", new Dictionary<Language, string> { { Language.English, "Voice threshold" }, { Language.Russian, "Порог громкости" } } },
            { "voicechat_menu", new Dictionary<Language, string> { { Language.English, "VoiceChat Menu" }, { Language.Russian, "Меню VoiceChat" } } },
            { "voice_activation", new Dictionary<Language, string> { { Language.English, "Voice Activation" }, { Language.Russian, "Активация голосом" } } },
            { "language", new Dictionary<Language, string> { { Language.English, "Language" }, { Language.Russian, "Язык" } } }
        };

        public static string T(string key)
        {
            if (texts.TryGetValue(key, out var dict))
            {
                if (dict.TryGetValue(CurrentLanguage, out var value))
                    return value;
            }
            return key;
        }

        public static void SetLanguage(string lang)
        {
            if (System.Enum.TryParse<Language>(lang, true, out var parsed))
                CurrentLanguage = parsed;
        }
    }
}
