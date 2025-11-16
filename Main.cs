using BepInEx;
using Steamworks;
using System;
using System.Net;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace DEPOVoiceChat
{
    /// <summary>
    /// main class handles plugin load, UI, voice capture, and network communication
    /// </summary>
    [BepInPlugin("ru.makorddev.depovoicechat", "Voicechat", "1.0.0")]
    public class Main : BaseUnityPlugin
    {
        // menu visibility and microphone dropdown state
        private bool showMenu = false;
        private Rect menuRect = new Rect(Screen.width - 500, 100, 400, 600);
        private bool micDropdownOpen = false;
        public static IPEndPoint ServerEP;

        private bool waitingKey = false;

        /// <summary>
        /// load settings, initialize devices and steam on plugin awake
        /// </summary>
        async void Awake()
        {
            Debug.Log("[VoiceChat] VoiceChat loaded.");

            try
            {
                var addr = await Dns.GetHostAddressesAsync("busiatep.ru");
                ServerEP = new IPEndPoint(addr[0], 6001);
            }
            catch (Exception ex)
            {
                Debug.LogError("[VoiceChat] DNS failed: " + ex);
            }

            SettingsManager.Load();
            VoiceManager.InitDevices();
            InitializeSteam();
            SceneManager.activeSceneChanged += OnSceneChanged;

            Localization.SetLanguage(SettingsManager.CurrentSettings.language);
        }

        /// <summary>
        /// connect to server, start microphone capture and receive voice
        /// handles reconnection
        /// </summary>
        async void Start()
        {
            VoiceManager.SetInstanceId(Guid.NewGuid().ToString());
            bool connected = await NetworkManager.Connect();

            if (!connected)
            {
                Debug.LogError("[VoiceChat] Failed to connect to server");
                return;
            }

            NetworkManager.OnReconnected += () =>
            {
                VoiceManager.RestartUdp();
            };

            if (VoiceManager.MicDevices.Length > 0)
            {
                int idx = SettingsManager.CurrentSettings.selectedMicIndex;
                VoiceManager.StartCapture(idx);
            }

            VoiceManager.StartReceiving();
        }

        /// <summary>
        /// cleanup all resources on destroy
        /// stop capturing and disconnect network
        /// </summary>
        void OnDestroy()
        {
            NetworkManager.Disconnect();
            VoiceManager.StopCapture();
            VoiceManager.StopReceiving();
            VoiceManager.StopVoiceStream();
        }

        /// <summary>
        /// handle input every frame
        /// toggle menu and push-to-talk key handling
        /// </summary>
        void Update()
        {
            SpeakingIndicator.UpdateSpeakingIndicators();
            if (SceneManager.GetActiveScene().name != "menus")
            {
                if (Input.GetKeyDown((KeyCode)SettingsManager.CurrentSettings.menuToggleKey))
                {
                    showMenu = !showMenu;
                    if (!showMenu) micDropdownOpen = false;
                }

                if (SettingsManager.CurrentSettings.micMode == MicMode.PushToTalk)
                {
                    if (Input.GetKeyDown((KeyCode)SettingsManager.CurrentSettings.pushToTalkKey))
                        VoiceManager.StartVoiceStream();
                    if (Input.GetKeyUp((KeyCode)SettingsManager.CurrentSettings.pushToTalkKey))
                        VoiceManager.StopVoiceStream();
                }
            }
        }

        /// <summary>
        /// draw the main UI window and dropdown if open
        /// block clicks from passing through window
        /// </summary>
        void OnGUI()
        {
            if (!showMenu) return;

            GUI.color = Color.white;
            GUI.enabled = true;

            menuRect = GUI.Window(0, menuRect, DrawClientMenu, Localization.T("voicechat_menu"));

            if (menuRect.Contains(Event.current.mousePosition))
            {
                if (Event.current.type == EventType.MouseDown || Event.current.type == EventType.MouseUp || Event.current.type == EventType.MouseDrag)
                {
                    Event.current.Use();
                }
            }

            if (micDropdownOpen)
                DrawMicDropdown();
        }

        /// <summary>
        /// draw dropdown menu to select microphone
        /// handles selection and restart capture if needed
        /// </summary>
        private void DrawMicDropdown()
        {
            float width = 260f;
            float itemHeight = 24f;
            float height = Mathf.Min(itemHeight * VoiceManager.MicDevices.Length, 6 * itemHeight);

            float dropdownX = menuRect.x - width;
            float dropdownY = menuRect.y + 190f;

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

                    micDropdownOpen = false;
                }
            }
        }

        /// <summary>
        /// yield until user presses a key for keybind
        /// callback receives the pressed key
        /// </summary>
        private System.Collections.IEnumerator WaitForKeyPressed(Action<KeyCode> callback)
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
        /// draw client menu with all settings
        /// includes volumes, microphone, keybinds, buffer, language
        /// </summary>
        private void DrawClientMenu(int windowID)
        {
            GUILayout.BeginVertical();

            GUILayout.Label($"{Localization.T("connected_clients")} {NetworkManager.ClientList.Count}");

            // player volume slider
            GUILayout.Label(Localization.T("volume_players"));
            float newPlayersVolume = GUILayout.HorizontalSlider(SettingsManager.CurrentSettings.playersVolume, 0f, 1f);
            if (Math.Abs(newPlayersVolume - SettingsManager.CurrentSettings.playersVolume) > 0.001f)
            {
                SettingsManager.CurrentSettings.playersVolume = newPlayersVolume;
                VoiceManager.UpdatePlayersVolume(newPlayersVolume);
                SettingsManager.Save();
            }

            // microphone volume slider
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
                micDropdownOpen = !micDropdownOpen;
            }

            GUILayout.Space(8);

            // keybinds section
            GUILayout.Label(Localization.T("keybinds"));

            GUILayout.BeginHorizontal();
            GUILayout.Label(Localization.T("open_close_menu"));
            if (GUILayout.Button(SettingsManager.CurrentSettings.menuToggleKey.ToString(), GUILayout.Width(100)))
            {
                if (!waitingKey)
                    StartCoroutine(WaitForKeyPressed(k => SettingsManager.CurrentSettings.menuToggleKey = k));
            }
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            GUILayout.Label(Localization.T("push_to_talk"));
            if (GUILayout.Button(SettingsManager.CurrentSettings.pushToTalkKey.ToString(), GUILayout.Width(100)))
            {
                if (!waitingKey)
                    StartCoroutine(WaitForKeyPressed(k => SettingsManager.CurrentSettings.pushToTalkKey = k));
            }
            GUILayout.EndHorizontal();

            //// microphone mode selection and threshold
            //GUILayout.Space(8);
            //GUILayout.Label(Localization.T("microphone_mode"));
            //string[] modes = { Localization.T("push_to_talk"), Localization.T("voice_activation") };
            //int selectedMode = (int)SettingsManager.CurrentSettings.micMode;
            //int newSelectedMode = GUILayout.Toolbar(selectedMode, modes);
            //if (newSelectedMode != selectedMode)
            //{
            //    SettingsManager.CurrentSettings.micMode = (MicMode)newSelectedMode;
            //    SettingsManager.Save();
            //}

            //if (SettingsManager.CurrentSettings.micMode == MicMode.VoiceActivation)
            //{
            //    GUILayout.Label($"{Localization.T("threshold")}: {SettingsManager.CurrentSettings.voiceThresholdDb} dB");
            //    float oldThreshold = SettingsManager.CurrentSettings.voiceThresholdDb;
            //    float newThreshold = GUILayout.HorizontalSlider(oldThreshold, -50f, -5f);
            //    if (Math.Abs(newThreshold - oldThreshold) > 0.001f)
            //    {
            //        SettingsManager.CurrentSettings.voiceThresholdDb = newThreshold;
            //        SettingsManager.Save();
            //    }
            //}

            GUILayout.Space(8);

            // buffer size slider
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

            // language selection toolbar
            GUILayout.Label(Localization.T("language"));
            string[] langs = { "English", "Русский", "Español", "中文", "日本語" };
            int langIdx = Array.IndexOf(langs, SettingsManager.CurrentSettings.language);
            if (langIdx == -1) langIdx = 0;

            int newLangIdx = GUILayout.Toolbar(langIdx, langs);
            if (newLangIdx != langIdx)
            {
                SettingsManager.CurrentSettings.language = langs[newLangIdx];
                Localization.SetLanguage(SettingsManager.CurrentSettings.language);
                SpeakingIndicator.UpdateSpeakText();
                SettingsManager.Save();
            }

            // close menu button
            if (GUILayout.Button(Localization.T("close")))
            {
                showMenu = false;
                micDropdownOpen = false;
            }

            GUILayout.EndVertical();

            GUI.DragWindow();
        }

        /// <summary>
        /// initialize Steam API for plugin
        /// logs errors if initialization fails
        /// </summary>
        private void InitializeSteam()
        {
            try
            {
                if (!SteamAPI.Init()) Debug.LogError("[VoiceChat] SteamAPI init failed!");
            }
            catch { Debug.LogError("[VoiceChat] Error occured when SteamAPI init."); }
        }

        /// <summary>
        /// handle scene change events
        /// sends info to server and adds dispatcher if missing
        /// </summary>
        private void OnSceneChanged(Scene oldScene, Scene newScene)
        {
            Task.Run(async () =>
            {
                string msg = $"INFO|{SteamUser.GetSteamID().m_SteamID}|{SteamFriends.GetPersonaName()}|{newScene.name}";
                await NetworkManager.SendMessage(msg);
            });

            if (GameObject.Find("Dispatcher") != null)
                return;

            var dispatcherObj = new GameObject("Dispatcher");
            dispatcherObj.AddComponent<UnityMainThreadDispatcher>();
            DontDestroyOnLoad(dispatcherObj);
        }
    }
}
