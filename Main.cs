using BepInEx;
using Steamworks;
using System;
using System.Net;
using System.Threading;
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
        public static IPEndPoint ServerEP;
        private readonly CancellationTokenSource cts = new CancellationTokenSource();
        private UI ui;

        /// <summary>
        /// load settings, initialize devices and steam on plugin awake
        /// includes DNS retry, device safety and error logging
        /// </summary>
        async void Awake()
        {
            Debug.Log("[VoiceChat] VoiceChat loaded.");

            try
            {
                IPAddress[] addr = null;
                int retries = 3;
                while (retries-- > 0)
                {
                    try
                    {
                        addr = await Dns.GetHostAddressesAsync("busiatep.ru");
                        if (addr.Length > 0) break;
                    }
                    catch { await Task.Delay(1000); }
                }

                if (addr == null || addr.Length == 0)
                {
                    Debug.LogError("[VoiceChat] DNS failed after retries.");
                    ServerEP = new IPEndPoint(IPAddress.Loopback, 6001); // fallback
                }
                else ServerEP = new IPEndPoint(addr[0], 6001);
            }
            catch (Exception ex)
            {
                Debug.LogError("[VoiceChat] DNS exception: " + ex);
                ServerEP = new IPEndPoint(IPAddress.Loopback, 6001);
            }

            try { SettingsManager.Load(); } catch { Debug.LogError("[VoiceChat] Failed to load settings."); }

            try { VoiceManager.InitDevices(); } catch { Debug.LogError("[VoiceChat] Failed to init devices."); }

            InitializeSteam();
            SceneManager.activeSceneChanged += OnSceneChanged;

            try { Localization.SetLanguage(SettingsManager.CurrentSettings.language); }
            catch { Debug.LogWarning("[VoiceChat] Failed to set language."); }

            var uiObj = new GameObject("VoiceChatUI");
            ui = uiObj.AddComponent<UI>();
            DontDestroyOnLoad(uiObj);
        }

        /// <summary>
        /// connect to server, start microphone capture and receive voice
        /// includes reconnect, exception handling and safe mic start
        /// </summary>
        async void Start()
        {
            VoiceManager.SetInstanceId(Guid.NewGuid().ToString());

            bool connected = false;
            int attempts = 3;
            while (!connected && attempts-- > 0)
            {
                try { connected = await NetworkManager.Connect(); }
                catch (Exception ex) { Debug.LogWarning("[VoiceChat] Connect attempt failed: " + ex); }
                if (!connected) await Task.Delay(2000);
            }

            if (!connected) { Debug.LogError("[VoiceChat] Failed to connect to server after retries."); return; }

            NetworkManager.OnReconnected += () =>
            {
                try { VoiceManager.RestartUdp(); }
                catch (Exception ex) { Debug.LogWarning("[VoiceChat] Failed to restart UDP: " + ex); }
            };

            try
            {
                if (VoiceManager.MicDevices.Length > 0)
                {
                    int idx = Mathf.Clamp(SettingsManager.CurrentSettings.selectedMicIndex, 0, VoiceManager.MicDevices.Length - 1);
                    VoiceManager.StartCapture(idx);
                }
            }
            catch (Exception ex) { Debug.LogError("[VoiceChat] Failed to start mic capture: " + ex); }

            try { await VoiceManager.StartReceiving(cts.Token); }
            catch (Exception ex) { Debug.LogError("[VoiceChat] Failed to start receiving voice: " + ex); }
        }

        /// <summary>
        /// cleanup all resources on destroy
        /// ensures safe stopping of all streams and network
        /// </summary>
        void OnDestroy()
        {
            try { cts?.Cancel(); } catch { }
            try { NetworkManager.Disconnect(); } catch { }
            try { VoiceManager.StopCapture(); } catch { }
            try { VoiceManager.StopReceiving(); } catch { }
            try { VoiceManager.StopVoiceStream(); } catch { }

            var dispatcher = GameObject.Find("Dispatcher");
            if (dispatcher != null)
            {
                try { Destroy(dispatcher); } catch { }
            }
        }

        /// <summary>
        /// handle input every frame
        /// toggle menu, push-to-talk, and update speaking indicators
        /// includes safety checks for scene and network
        /// </summary>
        void Update()
        {
            try
            {
                SpeakingIndicator.UpdateSpeakingIndicators();
                ui?.UpdateUI();
            }
            catch (Exception ex)
            {
                Debug.LogError("[VoiceChat] Update error: " + ex);
            }
        }

        /// <summary>
        /// draw UI window
        /// </summary>
        void OnGUI()
        {
            ui?.DrawUI();
        }

        /// <summary>
        /// handle scene change events
        /// includes safe message send and dispatcher initialization
        /// </summary>
        private void OnSceneChanged(Scene oldScene, Scene newScene)
        {
            Task.Run(async () =>
            {
                try
                {
                    string msg = $"INFO|{SteamUser.GetSteamID().m_SteamID}|{SteamFriends.GetPersonaName()}|{newScene.name}";
                    await NetworkManager.SendMessage(msg);
                }
                catch { Debug.LogWarning("[VoiceChat] Failed to send scene info."); }
            });

            if (GameObject.Find("Dispatcher") == null)
            {
                var dispatcherObj = new GameObject("Dispatcher");
                try { dispatcherObj.AddComponent<UnityMainThreadDispatcher>(); } catch { }
                DontDestroyOnLoad(dispatcherObj);
            }
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
            catch (Exception ex) { Debug.LogError("[VoiceChat] SteamAPI exception: " + ex); }
        }
    }
}
