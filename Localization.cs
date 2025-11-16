using System.Collections.Generic;

namespace DEPOVoiceChat
{
    /// <summary>
    /// handles multi-language localization for the voice chat
    /// provides translations for english, russian, spanish, chinese, and japanese
    /// </summary>
    public static class Localization
    {
        /// <summary>
        /// supported languages
        /// </summary>
        public enum Language { English, Russian, Spanish, French, Chinese, Japanese }

        /// <summary>
        /// current language used for localization
        /// </summary>
        public static Language CurrentLanguage { get; private set; } = Language.English;

        /// <summary>
        /// dictionary of keys and their translations for each language
        /// contains all ui strings and tips
        /// </summary>
        // в словаре texts добавляем переводы для французского языка
        private static readonly Dictionary<string, Dictionary<Language, string>> texts = new Dictionary<string, Dictionary<Language, string>>()
        {
            // displays number of connected clients
            { "connected_clients", new Dictionary<Language, string> {
                { Language.English, "Connected clients:" },
                { Language.Russian, "Подключено клиентов:" },
                { Language.Spanish, "Clientes conectados:" },
                { Language.Chinese, "已连接的客户端：" },
                { Language.Japanese, "接続中のクライアント：" },
                { Language.French, "Clients connectés :" }
            }},
            // controls for player volume
            { "volume_players", new Dictionary<Language, string> {
                { Language.English, "Volume players" },
                { Language.Russian, "Громкость игроков" },
                { Language.Spanish, "Volumen de jugadores" },
                { Language.Chinese, "玩家音量" },
                { Language.Japanese, "プレイヤーの音量" },
                { Language.French, "Volume des joueurs" }
            }},
            // controls for microphone volume
            { "volume_microphone", new Dictionary<Language, string> {
                { Language.English, "Volume microphone" },
                { Language.Russian, "Громкость микрофона" },
                { Language.Spanish, "Volumen del micrófono" },
                { Language.Chinese, "麦克风音量" },
                { Language.Japanese, "マイクの音量" },
                { Language.French, "Volume du micro" }
            }},
            // option to hear own voice
            { "hear_myself", new Dictionary<Language, string> {
                { Language.English, "Hear myself" },
                { Language.Russian, "Слушать себя" },
                { Language.Spanish, "Escucharme a mí mismo" },
                { Language.Chinese, "听自己的声音" },
                { Language.Japanese, "自分の声を聞く" },
                { Language.French, "S'entendre soi-même" }
            }},
            // selects which microphone to use
            { "select_microphone", new Dictionary<Language, string> {
                { Language.English, "Select microphone" },
                { Language.Russian, "Выбрать микрофон" },
                { Language.Spanish, "Seleccionar micrófono" },
                { Language.Chinese, "选择麦克风" },
                { Language.Japanese, "マイクを選択" },
                { Language.French, "Sélectionner le micro" }
            }},
            // keybinding section title
            { "keybinds", new Dictionary<Language, string> {
                { Language.English, "Keybinds:" },
                { Language.Russian, "Назначение клавиш:" },
                { Language.Spanish, "Asignación de teclas:" },
                { Language.Chinese, "按键绑定：" },
                { Language.Japanese, "キー割り当て：" },
                { Language.French, "Raccourcis clavier :" }
            }},
            // button label for opening or closing menu
            { "open_close_menu", new Dictionary<Language, string> {
                { Language.English, "Open/Close menu" },
                { Language.Russian, "Открыть/Закрыть меню" },
                { Language.Spanish, "Abrir/Cerrar menú" },
                { Language.Chinese, "打开/关闭菜单" },
                { Language.Japanese, "メニューを開閉" },
                { Language.French, "Ouvrir/Fermer le menu" }
            }},
            // button label for push-to-talk key
            { "push_to_talk", new Dictionary<Language, string> {
                { Language.English, "Push-to-talk" },
                { Language.Russian, "Нажать, чтобы разговаривать" },
                { Language.Spanish, "Pulsar para hablar" },
                { Language.Chinese, "按键发言" },
                { Language.Japanese, "プッシュ・トゥ・トーク" },
                { Language.French, "Appuyer pour parler" }
            }},
            // general close button
            { "close", new Dictionary<Language, string> {
                { Language.English, "Close" },
                { Language.Russian, "Закрыть" },
                { Language.Spanish, "Cerrar" },
                { Language.Chinese, "关闭" },
                { Language.Japanese, "閉じる" },
                { Language.French, "Fermer" }
            }},
            // label for selecting microphone mode
            { "microphone_mode", new Dictionary<Language, string> {
                { Language.English, "Microphone mode:" },
                { Language.Russian, "Режим микрофона:" },
                { Language.Spanish, "Modo micrófono:" },
                { Language.Chinese, "麦克风模式：" },
                { Language.Japanese, "マイクモード：" },
                { Language.French, "Mode micro :" }
            }},
            // threshold setting for voice activation
            { "threshold", new Dictionary<Language, string> {
                { Language.English, "Voice threshold" },
                { Language.Russian, "Порог громкости" },
                { Language.Spanish, "Umbral de voz" },
                { Language.Chinese, "语音激活阈值" },
                { Language.Japanese, "音声認識しきい値" },
                { Language.French, "Seuil de voix" }
            }},
            // main voicechat menu title
            { "voicechat_menu", new Dictionary<Language, string> {
                { Language.English, "Voicechat Menu" },
                { Language.Russian, "Меню голосового чата" },
                { Language.Spanish, "Menú de Voicechat" },
                { Language.Chinese, "语音聊天菜单" },
                { Language.Japanese, "ボイスチャットメニュー" },
                { Language.French, "Menu de chat vocal" }
            }},
            // option for voice activation
            { "voice_activation", new Dictionary<Language, string> {
                { Language.English, "Voice Activation" },
                { Language.Russian, "Активация голосом" },
                { Language.Spanish, "Activación por voz" },
                { Language.Chinese, "语音激活" },
                { Language.Japanese, "音声アクティベーション" },
                { Language.French, "Activation vocale" }
            }},
            // buffer size label
            { "buffer_size", new Dictionary<Language, string> {
                { Language.English, "Buffer size (ms)" },
                { Language.Russian, "Размер буфера (мс)" },
                { Language.Spanish, "Tamaño del búfer (ms)" },
                { Language.Chinese, "缓冲区大小（毫秒）" },
                { Language.Japanese, "バッファサイズ（ms）" },
                { Language.French, "Taille du tampon (ms)" }
            }},
            // label showing current buffer
            { "buffer_current", new Dictionary<Language, string> {
                { Language.English, "Current buffer:" },
                { Language.Russian, "Текущий буфер:" },
                { Language.Spanish, "Búfer actual:" },
                { Language.Chinese, "当前缓冲：" },
                { Language.Japanese, "現在のバッファ：" },
                { Language.French, "Tampon actuel :" }
            }},
            // language selection label
            { "language", new Dictionary<Language, string> {
                { Language.English, "Language" },
                { Language.Russian, "Язык" },
                { Language.Spanish, "Idioma" },
                { Language.Chinese, "语言" },
                { Language.Japanese, "言語" },
                { Language.French, "Langue" }
            }},
            // speaking text
            { "speaking", new Dictionary<Language, string> {
                { Language.English, "Speaking..." },
                { Language.Russian, "Говорит..." },
                { Language.Spanish, "Hablando..." },
                { Language.Chinese, "正在讲话..." },
                { Language.Japanese, "話しています..." },
                { Language.French, "Parle..." }
            }},
        };

        /// <summary>
        /// returns the localized string for a key based on current language
        /// returns key itself if no translation is found
        /// </summary>
        /// <param name="key">text identifier</param>
        public static string T(string key)
        {
            if (texts.TryGetValue(key, out var dict))
            {
                if (dict.TryGetValue(CurrentLanguage, out var value))
                    return value;
            }
            return key;
        }

        /// <summary>
        /// sets current language using string name
        /// defaults to english if name is unknown
        /// </summary>
        /// <param name="lang">language name like "English", "Русский", "Español", "中文", "日本語"</param>
        public static void SetLanguage(string lang)
        {
            switch (lang)
            {
                case "English": CurrentLanguage = Language.English; break;
                case "Russian (Русский)": CurrentLanguage = Language.Russian; break;
                case "Spanish (Español)": CurrentLanguage = Language.Spanish; break;
                case "French (Français)": CurrentLanguage = Language.French; break;
                case "Chinese (中文)": CurrentLanguage = Language.Chinese; break;
                case "Japanese (日本語)": CurrentLanguage = Language.Japanese; break;
                default: CurrentLanguage = Language.English; break;
            }
        }
    }
}
