using BepInEx;
using CSCore;
using Steamworks;
using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Rendering.VirtualTexturing;
using UnityEngine.SceneManagement;

namespace DEPOVoiceChat
{
    [BepInPlugin("ru.makorddev.depovoicechat", "DEPO VoiceChat", "1.0.0")]
    public class Main : BaseUnityPlugin
    {
        private bool showMenu = false;
        private Rect menuRect = new Rect(100, 100, 400, 340);
        private bool lockPlayerControl = false;
        private bool micDropdownOpen = false;
        private bool streaming = false;
        private Thread udpThread;
        private UdpClient udpClient;
        private Thread udpReceiveThread;
        private bool receiving = false;
        private UdpClient udpReceiver;

        void Awake()
        {
            Debug.Log("[VoiceChat] VoiceChat loaded.");
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
            StartReceiving();
        }

        void OnDestroy()
        {
            NetworkManager.Disconnect();
            VoiceManager.StopCapture();
            StopReceiving();
        }

        void Update()
        {
            if (Input.GetKeyDown(KeyCode.RightAlt) && SceneManager.GetActiveScene().name != "menus")
            {
                showMenu = !showMenu;
                lockPlayerControl = showMenu;
                Cursor.lockState = showMenu ? CursorLockMode.None : CursorLockMode.Locked;
                Cursor.visible = showMenu;
                if (!showMenu) micDropdownOpen = false;
            }
            if (Input.GetKeyDown(KeyCode.R))
                StartVoiceStream();
            if (Input.GetKeyUp(KeyCode.R))
                StopVoiceStream();
        }

        private async void StartVoiceStream()
        {
            if (streaming) return;
            streaming = true;
            Debug.Log("[VoiceChat] Запрос на UDP отправку...");

            await NetworkManager.SendMessage("UDP_REQUEST");

            bool ok = await NetworkManager.WaitForResponse("UDP_OK", 2000);
            if (!ok)
            {
                Debug.LogError("[VoiceChat] UDP согласование не удалось.");
                streaming = false;
                return;
            }

            Debug.Log("[VoiceChat] UDP согласование успешно, начинаем передачу звука.");

            udpThread = new Thread(() => SendAudioLoop());
            udpThread.IsBackground = true;
            udpThread.Start();
        }

        private void StopVoiceStream()
        {
            if (!streaming) return;
            streaming = false;

            try
            {
                udpClient?.Close();
                udpThread?.Join(500);
                udpThread = null;
            }
            catch (Exception ex)
            {
                Debug.LogError("[VoiceChat] Ошибка при остановке UDP-потока: " + ex.Message);
            }
        }

        private void StartReceiving()
        {
            if (receiving) return;

            try
            {
                udpReceiver = new UdpClient(6001); 
                receiving = true;
                udpReceiveThread = new Thread(ReceiveAudioLoop);
                udpReceiveThread.IsBackground = true;
                udpReceiveThread.Start();
                Debug.Log("[VoiceChat] UDP приём звука запущен.");
            }
            catch (Exception ex)
            {
                Debug.LogError("[VoiceChat] Не удалось запустить UDP приём: " + ex.Message);
            }
        }

        private void StopReceiving()
        {
            try
            {
                receiving = false;
                udpReceiver?.Close();
                udpReceiveThread?.Join(500);
                udpReceiveThread = null;
                Debug.Log("[VoiceChat] UDP приём звука остановлен.");
            }
            catch (Exception ex)
            {
                Debug.LogError("[VoiceChat] Ошибка при остановке UDP приёма: " + ex.Message);
            }
        }

        private void ReceiveAudioLoop()
        {
            try
            {
                IPEndPoint remoteEP = new IPEndPoint(IPAddress.Any, 0);
                var format = new CSCore.WaveFormat(48000, 16, 1); 

                var buffer = new CSCore.Streams.WriteableBufferingSource(format)
                {
                    FillWithZeros = false
                };

                using (var playback = new CSCore.SoundOut.WasapiOut())
                {
                    playback.Initialize(buffer.ToSampleSource().ToWaveSource(16));
                    playback.Volume = SettingsManager.CurrentSettings.playersVolume;
                    playback.Play();

                    Debug.Log("[VoiceChat] UDP приём и воспроизведение звука запущены.");

                    while (receiving)
                    {
                        byte[] data = udpReceiver.Receive(ref remoteEP);
                        if (data != null && data.Length > 0)
                        {
                            buffer.Write(data, 0, data.Length);
                        }

                        Thread.Sleep(2);
                    }

                    playback.Stop();
                }
            }
            catch (SocketException)
            {
                if (receiving)
                    Debug.LogError("[VoiceChat] UDP приём остановлен из-за ошибки сокета.");
            }
            catch (Exception ex)
            {
                Debug.LogError("[VoiceChat] Ошибка UDP-приёма: " + ex.Message);
            }
        }

        private void SendAudioLoop()
        {
            try
            {
                udpClient = new UdpClient();
                IPEndPoint endPoint = new IPEndPoint(Dns.GetHostAddresses("busiatep.ru")[0], 6001); 

                using (var capture = new CSCore.SoundIn.WasapiCapture())
                {
                    capture.Initialize();
                    var soundInSource = new CSCore.Streams.SoundInSource(capture) { FillWithZeros = false };
                    var waveSource = soundInSource.ToSampleSource().ToWaveSource(16);

                    byte[] buffer = new byte[2048];
                    soundInSource.DataAvailable += (s, e) =>
                    {
                        int read = waveSource.Read(buffer, 0, buffer.Length);
                        if (read > 0 && streaming)
                            udpClient.Send(buffer, read, endPoint);
                    };

                    capture.Start();
                    Debug.Log("[VoiceChat] Захват микрофона и передача по UDP начались.");

                    while (streaming)
                        Thread.Sleep(20);

                    capture.Stop();
                }
            }
            catch (Exception ex)
            {
                Debug.LogError("[VoiceChat] Ошибка UDP-захвата: " + ex.Message);
            }
        }

        void OnGUI()
        {
            if (showMenu)
            {         
                GUI.color = new Color(59, 59, 59, 1f);   
                menuRect = GUI.Window(0, menuRect, DrawClientMenu, "VoiceChat Menu");             
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
        private GUIStyle MakeSolidStyle(Color color)
        {
            Texture2D tex = new Texture2D(1, 1);
            tex.SetPixel(0, 0, color);
            tex.Apply();

            GUIStyle style = new GUIStyle(GUI.skin.box);
            style.normal.background = tex;
            return style;
        }

    }
}
