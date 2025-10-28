using BepInEx;
using UnityEngine;
using UnityEngine.SceneManagement;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using System.Threading;
using System.Collections.Generic;
using Steamworks;

namespace DEPOVoiceChat
{
    [BepInPlugin("ru.mxyffel_makordikrom.depovoicechat", "DEPO Voice Chat", "1.3.0")]
    public class Main : BaseUnityPlugin
    {
        private TcpClient tcpClient;
        private NetworkStream stream;
        private CancellationTokenSource cts;
        private string serverIp = "busiatep.ru";
        private int serverPort = 6000;
        private bool isConnecting = false;

        private Dictionary<string, string> clientList = new Dictionary<string, string>();
        private int heartbeatInterval = 5000;

        private string steamId = "";
        private string steamName = "";

        private bool showMenu = false;
        private Rect menuRect = new Rect(100, 100, 400, 400);
        private Vector2 scrollPos = Vector2.zero;
        private bool lockPlayerControl = false;

        private bool hearSelf = true;
        private float selfVolume = 1f;
        private float playersVolume = 1f;
        private string[] micDevices;
        private int selectedMicIndex = 0;
        private AudioSource selfAudioSource;
        private string micName = "";
        private bool micDropdownOpen = false;

        void Awake()
        {
            Logger.LogInfo("Voice Chat мод загружен.");
            InitializeSteam();
            SceneManager.activeSceneChanged += OnSceneChanged;
        }

        void Start()
        {
            ConnectToServer();

            micDevices = Microphone.devices;
            if (micDevices.Length > 0)
            {
                selectedMicIndex = 0;
                StartMicrophone(micDevices[selectedMicIndex]);
            }
        }

        void Update()
        {
            if ((Input.GetKeyDown(KeyCode.RightAlt) || Input.GetKeyDown(KeyCode.LeftAlt)) && SceneManager.GetActiveScene().name != "menus")
            {
                showMenu = !showMenu;
                lockPlayerControl = showMenu;

                if (showMenu)
                {
                    Cursor.lockState = CursorLockMode.None;
                    Cursor.visible = true;
                }
                else
                {
                    Cursor.lockState = CursorLockMode.Locked;
                    Cursor.visible = false;
                }
            }
        }

        void OnGUI()
        {
            if (showMenu)
            {
                menuRect = GUI.Window(0, menuRect, DrawClientMenu, "VoiceChat Menu");
            }
        }
        private void DrawClientMenu(int windowID)
        {
            GUILayout.BeginVertical();
            GUILayout.Label($"Подключено клиентов: {clientList.Count}");

            scrollPos = GUILayout.BeginScrollView(scrollPos, GUILayout.Height(150));
            foreach (var kv in clientList)
            {
                GUILayout.BeginHorizontal("box");
                GUILayout.Label($"Имя: {kv.Value}");
                GUILayout.EndHorizontal();
            }
            GUILayout.EndScrollView();

            GUILayout.Space(10);

            GUILayout.Label("Громкость игроков: " + playersVolume.ToString("0.00"));
            playersVolume = GUILayout.HorizontalSlider(playersVolume, 0f, 1f);

            GUILayout.Label("Громкость себя: " + selfVolume.ToString("0.00"));
            selfVolume = GUILayout.HorizontalSlider(selfVolume, 0f, 1f);

            hearSelf = GUILayout.Toggle(hearSelf, "Услышать себя");
            if (selfAudioSource != null)
                selfAudioSource.mute = !hearSelf;

            GUILayout.Label("Выбрать микрофон:");
            if (micDevices != null && micDevices.Length > 0)
            {
                if (GUILayout.Button(micDevices[selectedMicIndex])) 
                {
                    micDropdownOpen = !micDropdownOpen;
                }

                if (micDropdownOpen)
                {
                    foreach (var device in micDevices)
                    {
                        if (GUILayout.Button(device))
                        {
                            selectedMicIndex = System.Array.IndexOf(micDevices, device);
                            StartMicrophone(device);
                            micDropdownOpen = false;
                        }
                    }
                }
            }
            else GUILayout.Label("Нет доступных микрофонов");

            GUILayout.Space(10);
            if (GUILayout.Button("Закрыть"))
            {
                showMenu = false;
                lockPlayerControl = false;
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
            }

            GUILayout.EndVertical();
            GUI.DragWindow();
        }
        private void StartMicrophone(string device)
        {
            micName = device;
            if (selfAudioSource == null)
            {
                GameObject go = new GameObject("SelfMicAudio");
                selfAudioSource = go.AddComponent<AudioSource>();
                selfAudioSource.loop = true;
            }
            if (Microphone.IsRecording(micName)) Microphone.End(micName);
            selfAudioSource.clip = Microphone.Start(micName, true, 10, 44100);
            while (!(Microphone.GetPosition(micName) > 0)) { } 
            selfAudioSource.Play();
        }

        void LateUpdate()
        {
            if (selfAudioSource != null)
            {
                selfAudioSource.volume = hearSelf ? selfVolume : 0f;
            }
        }

        private void InitializeSteam()
        {
            try
            {
                if (!SteamAPI.Init()) Logger.LogError("SteamAPI.Init() не удалось!");
                else
                {
                    steamId = SteamUser.GetSteamID().m_SteamID.ToString();
                    steamName = SteamFriends.GetPersonaName();
                    Logger.LogInfo($"Steam инициализирован: {steamName} ({steamId})");
                }
            }
            catch (System.Exception ex)
            {
                Logger.LogError("Ошибка инициализации Steam: " + ex.Message);
            }
        }

        private void ConnectToServer()
        {
            if (isConnecting) return;
            isConnecting = true;
            cts = new CancellationTokenSource();

            Task.Run(async () =>
            {
                while (!cts.Token.IsCancellationRequested)
                {
                    if (tcpClient == null || !tcpClient.Connected)
                    {
                        try
                        {
                            tcpClient?.Close();
                            tcpClient = new TcpClient();
                            await tcpClient.ConnectAsync(serverIp, serverPort);
                            stream = tcpClient.GetStream();
                            Logger.LogInfo("TCP соединение установлено.");

                            await SendClientInfo();
                            _ = Task.Run(() => HeartbeatLoop(cts.Token));
                            _ = Task.Run(() => ReceiveLoop(cts.Token));

                            while (tcpClient.Connected && !cts.Token.IsCancellationRequested)
                                await Task.Delay(1000);
                        }
                        catch (System.Exception ex)
                        {
                            Logger.LogError("Ошибка TCP: " + ex.Message);
                        }
                    }

                    await Task.Delay(5000);
                }
            });
        }

        private void OnSceneChanged(Scene oldScene, Scene newScene)
        {
            Logger.LogInfo($"Смена сцены: {oldScene.name} -> {newScene.name}");
            Task.Run(async () =>
            {
                string infoMsg = $"INFO|{steamId}|{steamName}|{newScene.name}";
                await SendMessage(infoMsg);
            });
        }

        private async Task SendClientInfo()
        {
            string sceneName = SceneManager.GetActiveScene().name;
            string infoMsg = $"INFO|{steamId}|{steamName}|{sceneName}";
            await SendMessage(infoMsg);
        }

        private async Task HeartbeatLoop(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                await Task.Delay(heartbeatInterval);
                await SendMessage("HEARTBEAT");
            }
        }

        private async Task ReceiveLoop(CancellationToken token)
        {
            byte[] buffer = new byte[4096];
            while (!token.IsCancellationRequested)
            {
                if (stream.DataAvailable)
                {
                    int read = await stream.ReadAsync(buffer, 0, buffer.Length, token);
                    if (read > 0)
                    {
                        string message = Encoding.UTF8.GetString(buffer, 0, read);
                        HandleServerMessage(message);
                    }
                }
                else await Task.Delay(50);
            }
        }
        private void HandleServerMessage(string message)
        {
            if (message.StartsWith("CLIENTS"))
            {
                clientList.Clear();
                string[] parts = message.Split('|');
                if (parts.Length > 1)
                {
                    foreach (var c in parts[1].Split(','))
                    {
                        string[] kv = c.Split(':');
                        if (kv.Length == 2) clientList[kv[0]] = kv[1];
                    }
                }
                Logger.LogInfo("Обновлен список клиентов: " + clientList.Count);
            }
            else if (message.StartsWith("SETTINGS"))
                Logger.LogInfo("Получены настройки: " + message);
            else if (message.StartsWith("DISCONNECT"))
            {
                Logger.LogWarning("Сервер разорвал соединение: " + message.Substring("DISCONNECT|".Length));
                tcpClient?.Close();
            }
        }

        private async Task SendMessage(string msg)
        {
            if (tcpClient != null && tcpClient.Connected)
            {
                byte[] data = Encoding.UTF8.GetBytes(msg);
                try { await stream.WriteAsync(data, 0, data.Length); }
                catch { Logger.LogError("Не удалось отправить сообщение TCP."); }
            }
        }

        void OnDestroy()
        {
            cts?.Cancel();
            tcpClient?.Close();
            if (Microphone.IsRecording(micName)) Microphone.End(micName);
        }
    }
}
