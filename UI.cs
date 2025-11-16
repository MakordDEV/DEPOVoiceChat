using System;
using System.Threading;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace DEPOVoiceChat
{
    /// <summary>
    /// handles drawing and interacting with the voice chat UI
    /// includes menus, dropdowns, sliders, toggles and keybinds
    /// </summary>
    public class UI : MonoBehaviour
    {
        public bool ShowMenu { get; set; } = false;
        public Rect MenuRect { get; set; } = new Rect(Screen.width - 500, 100, 400, 600);
        public bool MicDropdownOpen { get; set; } = false;
        private bool waitingKey = false;
        private readonly CancellationTokenSource cts = new CancellationTokenSource();

        /// <summary>
        /// main update loop for UI
        /// handles toggle menu and push-to-talk input
        /// </summary>
        public void UpdateUI()
        {
            if (SceneManager.GetActiveScene().name != "menus")
            {
                if (Input.GetKeyDown((KeyCode)SettingsManager.CurrentSettings.menuToggleKey))
                {
                    ShowMenu = !ShowMenu;
                    if (!ShowMenu) MicDropdownOpen = false;
                }

                if (SettingsManager.CurrentSettings.micMode == MicMode.PushToTalk)
                {
                    if (Input.GetKeyDown((KeyCode)SettingsManager.CurrentSettings.pushToTalkKey))
                        _ = VoiceManager.StartVoiceStream(cts.Token);
                    if (Input.GetKeyUp((KeyCode)SettingsManager.CurrentSettings.pushToTalkKey))
                        VoiceManager.StopVoiceStream();
                }
            }
        }

        /// <summary>
        /// draws the UI window and blocks clicks from passing through
        /// </summary>
        public void DrawUI()
        {
            if (!ShowMenu) return;

            GUI.color = Color.white;
            GUI.enabled = true;

            MenuRect = GUI.Window(0, MenuRect, DrawClientMenu, Localization.T("voicechat_menu"));

            if (MenuRect.Contains(Event.current.mousePosition))
            {
                if (Event.current.type == EventType.MouseDown || Event.current.type == EventType.MouseUp || Event.current.type == EventType.MouseDrag)
                    Event.current.Use();
            }

            if (MicDropdownOpen)
                DrawMicDropdown();
        }

        /// <summary>
        /// draw microphone dropdown menu
        /// handles selection and restart capture if needed
        /// </summary>
        private void DrawMicDropdown()
        {
            float width = 260f;
            float itemHeight = 24f;
            float height = Mathf.Min(itemHeight * VoiceManager.MicDevices.Length, 6 * itemHeight);

            float dropdownX = MenuRect.x - width;
            float dropdownY = MenuRect.y + 190f;

            GUI.Box(new Rect(dropdownX, dropdownY, width, height), "");

            for (int i = 0; i < VoiceManager.MicDevices.Length; i++)
            {
                Rect btnRect = new Rect(dropdownX, dropdownY + i * itemHeight, width, itemHeight);
                if (GUI.Button(btnRect, VoiceManager.MicDevices[i]))
                {
                    SettingsManager.CurrentSettings.selectedMicIndex = i;
                    SettingsManager.Save();

                    VoiceManager.StopCapture();
                    if (SettingsManager.CurrentSettings.hearSelf)
                        VoiceManager.StartCapture(i);

                    MicDropdownOpen = false;
                }
            }
        }

        /// <summary>
        /// yield until user presses a key for keybind
        /// callback receives the pressed key
        /// </summary>
        public System.Collections.IEnumerator WaitForKeyPressed(Action<KeyCode> callback)
        {
            waitingKey = true;

            while (waitingKey)
            {
                foreach (KeyCode kcode in Enum.GetValues(typeof(KeyCode)))
                {
                    if (Input.GetKeyDown(kcode))
                    {
                        callback(kcode);
                        SettingsManager.Save();
                        waitingKey = false;
                        break;
                    }
                }
                yield return null;
            }
        }

        /// <summary>
        /// draw main client menu with sliders, toggles, keybinds, buffer and language selection
        /// </summary>
        private void DrawClientMenu(int windowID)
        {
            GUILayout.BeginVertical();

            GUILayout.Label($"{Localization.T("connected_clients")} {NetworkManager.ClientList.Count}");

            // player volume
            GUILayout.Label(Localization.T("volume_players"));
            float newPlayersVolume = GUILayout.HorizontalSlider(SettingsManager.CurrentSettings.playersVolume, 0f, 1f);
            if (Math.Abs(newPlayersVolume - SettingsManager.CurrentSettings.playersVolume) > 0.001f)
            {
                SettingsManager.CurrentSettings.playersVolume = newPlayersVolume;
                VoiceManager.UpdatePlayersVolume(newPlayersVolume);
                SettingsManager.Save();
            }

            // microphone volume
            GUILayout.Label(Localization.T("volume_microphone"));
            float newSelfVolume = GUILayout.HorizontalSlider(SettingsManager.CurrentSettings.selfVolume, 0f, 1f);
            if (Math.Abs(newSelfVolume - SettingsManager.CurrentSettings.selfVolume) > 0.001f)
            {
                SettingsManager.CurrentSettings.selfVolume = newSelfVolume;
                VoiceManager.UpdateSelfVolume(newSelfVolume);
                SettingsManager.Save();
            }

            // hear myself toggle
            bool newHearSelf = GUILayout.Toggle(SettingsManager.CurrentSettings.hearSelf, Localization.T("hear_myself"));
            if (newHearSelf != SettingsManager.CurrentSettings.hearSelf)
            {
                SettingsManager.CurrentSettings.hearSelf = newHearSelf;
                SettingsManager.Save();

                VoiceManager.StopCapture();
                if (newHearSelf)
                    VoiceManager.StartCapture(SettingsManager.CurrentSettings.selectedMicIndex);
            }

            GUILayout.Space(8);

            // microphone selection button
            GUILayout.Label(Localization.T("select_microphone"));
            if (GUILayout.Button(VoiceManager.MicDevices.Length > 0 ? VoiceManager.MicDevices[SettingsManager.CurrentSettings.selectedMicIndex] : "No microphone", GUILayout.Width(260)))
            {
                MicDropdownOpen = !MicDropdownOpen;
            }

            GUILayout.Space(8);

            // keybinds
            GUILayout.Label(Localization.T("keybinds"));
            DrawKeybind("open_close_menu", SettingsManager.CurrentSettings.menuToggleKey, k => SettingsManager.CurrentSettings.menuToggleKey = k);
            DrawKeybind("push_to_talk", SettingsManager.CurrentSettings.pushToTalkKey, k => SettingsManager.CurrentSettings.pushToTalkKey = k);

            GUILayout.Space(8);

            // buffer size
            GUILayout.BeginHorizontal();
            GUILayout.Label(Localization.T("buffer_size"));
            int oldBuf = SettingsManager.CurrentSettings.bufferSizeMs;
            int newBuf = (int)GUILayout.HorizontalSlider(oldBuf, 10, 100);
            GUILayout.EndHorizontal();
            GUILayout.Label($"{Localization.T("buffer_current")} {newBuf} ms");

            if (newBuf != oldBuf)
            {
                SettingsManager.CurrentSettings.bufferSizeMs = newBuf;
                SettingsManager.Save();
            }

            GUILayout.Space(8);

            // language selection
            GUILayout.Label(Localization.T("language"));

            // добавляем французский язык
            string[] langs = { "English", "Russian (Русский)", "Spanish (Español)", "French (Français)", "Chinese (中文)", "Japanese (日本語)" };
            int langIdx = Array.IndexOf(langs, SettingsManager.CurrentSettings.language);
            if (langIdx == -1) langIdx = 0;

            // задаем 3 строки
            int columns = 3;
            int rows = Mathf.CeilToInt((float)langs.Length / columns);

            int newLangIdx = langIdx;
            for (int r = 0; r < rows; r++)
            {
                int start = r * columns;
                int end = Mathf.Min(start + columns, langs.Length);
                string[] rowLangs = new string[end - start];
                Array.Copy(langs, start, rowLangs, 0, rowLangs.Length);

                int selected = GUILayout.Toolbar(Array.IndexOf(rowLangs, langs[newLangIdx]), rowLangs);
                if (selected != Array.IndexOf(rowLangs, langs[newLangIdx]))
                {
                    newLangIdx = start + selected;
                }
            }

            if (newLangIdx != langIdx)
            {
                SettingsManager.CurrentSettings.language = langs[newLangIdx];
                Localization.SetLanguage(SettingsManager.CurrentSettings.language);
                SpeakingIndicator.UpdateSpeakText();
                SettingsManager.Save();
            }

            // close menu
            if (GUILayout.Button(Localization.T("close")))
            {
                ShowMenu = false;
                MicDropdownOpen = false;
            }

            GUILayout.EndVertical();
            GUI.DragWindow();
        }

        /// <summary>
        /// helper to draw a keybind line in the menu
        /// </summary>
        private void DrawKeybind(string labelKey, KeyCode currentKey, Action<KeyCode> onKeyPressed)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label(Localization.T(labelKey));
            if (GUILayout.Button(currentKey.ToString(), GUILayout.Width(100)))
            {
                if (!waitingKey)
                    StartCoroutine(WaitForKeyPressed(onKeyPressed));
            }
            GUILayout.EndHorizontal();
        }
    }
}
