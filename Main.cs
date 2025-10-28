using BepInEx;
using Steamworks;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace DEPOVoiceChat
{
    [BepInPlugin("ru.mxyffel_makordikrom.depovoicechat", "DEPO Voice Chat", "1.0.0")]
    public class Main : BaseUnityPlugin
    {
        private bool showMenu = false;
        private Rect menuRect = new Rect(100, 100, 400, 340);
        private bool lockPlayerControl = false;
        private bool micDropdownOpen = false;

        void Awake()
        {
            Debug.Log("Voice Chat mod loaded.");
            SettingsManager.Load();
            VoiceManager.InitDevices();
            InitializeSteam();
            SceneManager.activeSceneChanged += OnSceneChanged;
        }

        void Start()
        {
            NetworkManager.Connect();
            if (VoiceManager.MicDevices.Length > 0)
            {
                int idx = SettingsManager.CurrentSettings.selectedMicIndex;
                VoiceManager.StartCapture(idx);
            }
        }

        void OnDestroy()
        {
            VoiceManager.StopCapture();
        }

        void Update()
        {
            if ((Input.GetKeyDown(KeyCode.RightAlt) || Input.GetKeyDown(KeyCode.LeftAlt)) &&
                SceneManager.GetActiveScene().name != "menus")
            {
                showMenu = !showMenu;
                lockPlayerControl = showMenu;
                Cursor.lockState = showMenu ? CursorLockMode.None : CursorLockMode.Locked;
                Cursor.visible = showMenu;
                if (!showMenu) micDropdownOpen = false;
            }
        }

        void OnGUI()
        {
            if (showMenu) menuRect = GUI.Window(0, menuRect, DrawClientMenu, "VoiceChat Menu");
        }

        private void DrawClientMenu(int windowID)
        {
            GUILayout.BeginVertical();
            GUILayout.Label($"Connected clients: {NetworkManager.ClientList.Count}");

            GUILayout.Label("Volume players:");
            SettingsManager.CurrentSettings.playersVolume = GUILayout.HorizontalSlider(SettingsManager.CurrentSettings.playersVolume, 0f, 1f);

            GUILayout.Label("Volume microphone:");
            SettingsManager.CurrentSettings.selfVolume = GUILayout.HorizontalSlider(SettingsManager.CurrentSettings.selfVolume, 0f, 1f);

            bool newHearSelf = GUILayout.Toggle(SettingsManager.CurrentSettings.hearSelf, "Hear myself");
            if (newHearSelf != SettingsManager.CurrentSettings.hearSelf)
            {
                SettingsManager.CurrentSettings.hearSelf = newHearSelf;
                VoiceManager.StopCapture();
                if (newHearSelf) VoiceManager.StartCapture(SettingsManager.CurrentSettings.selectedMicIndex);
            }

            GUILayout.Space(8);
            GUILayout.Label("Select microphone:");
            if (GUILayout.Button(VoiceManager.MicDevices.Length > 0 ? VoiceManager.MicDevices[SettingsManager.CurrentSettings.selectedMicIndex] : "No microphone", GUILayout.Width(260)))
            {
                micDropdownOpen = !micDropdownOpen;
            }

            if (micDropdownOpen)
            {
                float width = 260;
                float itemHeight = 24f;
                float height = Mathf.Min(itemHeight * VoiceManager.MicDevices.Length, 6 * itemHeight);
                float offsetX = -256; 

                GUI.Box(new Rect(offsetX, menuRect.height - height, width, height), "");

                for (int i = 0; i < VoiceManager.MicDevices.Length; i++)
                {
                    Rect btnRect = new Rect(offsetX, menuRect.height - height + i * itemHeight, width, itemHeight);
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

            if (GUILayout.Button("Close"))
            {
                showMenu = false;
                micDropdownOpen = false;
                lockPlayerControl = false;
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
                SettingsManager.Save();
            }

            GUILayout.EndVertical();
            GUI.DragWindow();
        }

        private void InitializeSteam()
        {
            try
            {
                if (!SteamAPI.Init()) Debug.LogError("SteamAPI init failed!");
            }
            catch { Debug.LogError("Error occured when SteamAPI init."); }
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
