using BepInEx;
using Steamworks;
using System;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace DEPOVoiceChat
{
    [BepInPlugin("ru.makorddev.depovoicechat", "DEPO VoiceChat", "1.0.0")]
    public class Main : BaseUnityPlugin
    {
        private bool showMenu = false;
        private Rect menuRect = new Rect(100, 100, 400, 500);
        private bool micDropdownOpen = false;

        private string instanceId = null;

        void Awake()
        {
            Debug.Log("[VoiceChat] VoiceChat loaded.");
            SettingsManager.Load();
            VoiceManager.InitDevices();
            InitializeSteam();
            SceneManager.activeSceneChanged += OnSceneChanged;

            instanceId = Guid.NewGuid().ToString();

            Localization.SetLanguage(SettingsManager.CurrentSettings.language);
        }

        async void Start()
        {
            bool connected = await NetworkManager.Connect();

            if (!connected)
            {
                Debug.LogError("[VoiceChat] Не удалось подключиться к серверу!");
                return;
            }

            if (VoiceManager.MicDevices.Length > 0)
            {
                int idx = SettingsManager.CurrentSettings.selectedMicIndex;
                VoiceManager.StartCapture(idx);
            }
            VoiceManager.StartReceiving(instanceId);
        }

        void OnDestroy()
        {
            NetworkManager.Disconnect();
            VoiceManager.StopCapture();
            VoiceManager.StopReceiving();
            VoiceManager.StopVoiceStream();
        }

        void Update()
        {
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
                    VoiceManager.StopCapture();
                    if (SettingsManager.CurrentSettings.hearSelf)
                        VoiceManager.StartCapture(i);
                    micDropdownOpen = false;
                }
            }
        }

        private System.Collections.IEnumerator WaitForKeyPressed(Action<KeyCode> callback)
        {
            bool keySet = false;
            while (!keySet)
            {
                foreach (KeyCode kcode in Enum.GetValues(typeof(KeyCode)))
                {
                    if (Input.GetKeyDown(kcode))
                    {
                        callback(kcode);
                        keySet = true;
                        SettingsManager.Save();
                        break;
                    }
                }
                yield return null;
            }
        }

        private void DrawClientMenu(int windowID)
        {
            GUILayout.BeginVertical();

            GUILayout.Label($"{Localization.T("connected_clients")} {NetworkManager.ClientList.Count}");

            GUILayout.Label(Localization.T("volume_players"));
            SettingsManager.CurrentSettings.playersVolume = GUILayout.HorizontalSlider(SettingsManager.CurrentSettings.playersVolume, 0f, 1f);

            GUILayout.Label(Localization.T("volume_microphone"));
            SettingsManager.CurrentSettings.selfVolume = GUILayout.HorizontalSlider(SettingsManager.CurrentSettings.selfVolume, 0f, 1f);

            bool newHearSelf = GUILayout.Toggle(SettingsManager.CurrentSettings.hearSelf, Localization.T("hear_myself"));
            if (newHearSelf != SettingsManager.CurrentSettings.hearSelf)
            {
                SettingsManager.CurrentSettings.hearSelf = newHearSelf;
                VoiceManager.StopCapture();
                if (newHearSelf) VoiceManager.StartCapture(SettingsManager.CurrentSettings.selectedMicIndex);
            }

            GUILayout.Space(8);
            GUILayout.Label(Localization.T("select_microphone"));

            if (GUILayout.Button(VoiceManager.MicDevices.Length > 0 ? VoiceManager.MicDevices[SettingsManager.CurrentSettings.selectedMicIndex] : "No microphone", GUILayout.Width(260)))
            {
                micDropdownOpen = !micDropdownOpen;
            }

            GUILayout.Space(8);
            GUILayout.Label(Localization.T("keybinds"));

            GUILayout.BeginHorizontal();
            GUILayout.Label(Localization.T("open_close_menu"));
            if (GUILayout.Button(SettingsManager.CurrentSettings.menuToggleKey.ToString(), GUILayout.Width(100)))
            {
                StartCoroutine(WaitForKeyPressed(k => SettingsManager.CurrentSettings.menuToggleKey = k));
                SettingsManager.Save();
            }
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            GUILayout.Label(Localization.T("push_to_talk"));
            if (GUILayout.Button(SettingsManager.CurrentSettings.pushToTalkKey.ToString(), GUILayout.Width(100)))
            {
                StartCoroutine(WaitForKeyPressed(k => SettingsManager.CurrentSettings.pushToTalkKey = k));
                SettingsManager.Save();
            }
            GUILayout.EndHorizontal();

            GUILayout.Space(8);
            GUILayout.Label(Localization.T("microphone_mode"));

            string[] modes = { Localization.T("push_to_talk"), Localization.T("voice_activation") };
            int selectedMode = (int)SettingsManager.CurrentSettings.micMode;
            selectedMode = GUILayout.Toolbar(selectedMode, modes);
            SettingsManager.CurrentSettings.micMode = (MicMode)selectedMode;

            if (SettingsManager.CurrentSettings.micMode == MicMode.VoiceActivation)
            {
                GUILayout.Label($"{Localization.T("threshold")}: {SettingsManager.CurrentSettings.voiceThresholdDb} dB");
                SettingsManager.CurrentSettings.voiceThresholdDb = GUILayout.HorizontalSlider(SettingsManager.CurrentSettings.voiceThresholdDb, -10f, 50f);
            }

            GUILayout.Space(8);
            GUILayout.Label(Localization.T("language"));
            string[] langs = { "English", "Русский" };
            int langIdx = SettingsManager.CurrentSettings.language == "English" ? 0 : 1;

            int newLangIdx = GUILayout.Toolbar(langIdx, langs);
            if (newLangIdx != langIdx)
            {
                SettingsManager.CurrentSettings.language = langs[newLangIdx];
                Localization.SetLanguage(SettingsManager.CurrentSettings.language);
                SettingsManager.Save();
            }

            if (GUILayout.Button(Localization.T("close")))
            {
                showMenu = false;
                micDropdownOpen = false;
                SettingsManager.Save();
            }

            GUILayout.EndVertical();

            GUI.DragWindow();
        }

        private void InitializeSteam()
        {
            try
            {
                if (!SteamAPI.Init()) Debug.LogError("[VoiceChat] SteamAPI init failed!");
            }
            catch { Debug.LogError("[VoiceChat] Error occured when SteamAPI init."); }
        }

        private void OnSceneChanged(Scene oldScene, Scene newScene)
        {
            Task.Run(async () =>
            {
                string msg = $"INFO|{SteamUser.GetSteamID().m_SteamID}|{SteamFriends.GetPersonaName()}|{newScene.name}";
                await NetworkManager.SendMessage(msg);
            });
        }
    }
}
