using System;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace DEPOVoiceChat
{
    /// <summary>
    /// handles tcp connection, message sending, receiving and client list management
    /// manages reconnecting automatically if connection is lost
    /// </summary>
    public static class NetworkManager
    {
        public enum ConnectionState { Disconnected, Connecting, Connected }

        /// <summary>
        /// current state of tcp connection
        /// </summary>
        public static ConnectionState State { get; private set; } = ConnectionState.Disconnected;

        /// <summary>
        /// stores clients in format steamid:name
        /// </summary>
        public static Dictionary<string, string> ClientList { get; private set; } = new Dictionary<string, string>();
        public static TcpClient Client => tcpClient;
        public static NetworkStream Stream => stream;
        public static event Action OnReconnected;

        private static TcpClient tcpClient;
        private static NetworkStream stream;
        private static CancellationTokenSource cts;
        private static CancellationTokenSource monitorCts;

        private static string serverIp = "busiatep.ru";
        private static int serverPort = 6000;
        private static int heartbeatInterval = 5000;

        private static string lastMessage = "";
        private static readonly object msgLock = new object();

        private static bool reconnecting = false;

        /// <summary>
        /// connects to tcp server and starts heartbeat and receive loops
        /// sends initial client info and waits for clients list response
        /// </summary>
        public static async Task<bool> Connect()
        {
            if (State == ConnectionState.Connecting || State == ConnectionState.Connected)
                return State == ConnectionState.Connected;

            State = ConnectionState.Connecting;
            Cleanup();
            cts = new CancellationTokenSource();

            try
            {
                tcpClient = new TcpClient();
                await tcpClient.ConnectAsync(serverIp, serverPort);
                stream = tcpClient.GetStream();
                Debug.Log("[VoiceChat] TCP connected.");

                string info = $"INFO|{Steamworks.SteamUser.GetSteamID().m_SteamID}|{Steamworks.SteamFriends.GetPersonaName()}|{SceneManager.GetActiveScene().name}";
                await SendMessage(info);

                bool ok = await WaitForResponse("CLIENTS|", 2000);
                if (!ok) Debug.LogWarning("[VoiceChat] Client list not received yet.");

                _ = Task.Run(() => HeartbeatLoop(cts.Token));
                _ = Task.Run(() => ReceiveLoop(cts.Token));

                State = ConnectionState.Connected;
                StartMonitor();
                return true;
            }
            catch (Exception ex)
            {
                Debug.LogError("[VoiceChat] Connect error: " + ex.Message);
                Disconnect();
                StartReconnectLoop();
                return false;
            }
        }

        /// <summary>
        /// continuously checks connection state and starts reconnect if needed
        /// </summary>
        private static void StartMonitor()
        {
            monitorCts?.Cancel();
            monitorCts = new CancellationTokenSource();

            _ = Task.Run(async () =>
            {
                while (!monitorCts.IsCancellationRequested)
                {
                    await Task.Delay(3000);
                    if (State != ConnectionState.Connected && !reconnecting)
                    {
                        Debug.LogWarning("[VoiceChat] Connection lost. Trying to reconnect...");
                        StartReconnectLoop();
                    }
                }
            }, monitorCts.Token);
        }

        /// <summary>
        /// loop that tries to reconnect periodically until connection succeeds
        /// increases delay gradually but caps it at 10s
        /// invokes OnReconnected when connection restored
        /// </summary>
        private static void StartReconnectLoop()
        {
            if (reconnecting) return;
            reconnecting = true;

            _ = Task.Run(async () =>
            {
                int delay = 3000;
                while (State != ConnectionState.Connected)
                {
                    Debug.Log($"[VoiceChat] Reconnecting in {delay / 1000}s...");
                    await Task.Delay(delay);
                    bool ok = await Connect();
                    if (ok)
                    {
                        reconnecting = false;
                        Debug.Log("[VoiceChat] Reconnected successfully.");
                        OnReconnected?.Invoke();
                        return;
                    }

                    delay = Math.Min(delay + 3000, 10000);
                }
                reconnecting = false;
            });
        }

        /// <summary>
        /// sends a tcp message if connected
        /// starts reconnect if send fails
        /// </summary>
        public static async Task SendMessage(string msg)
        {
            if (tcpClient != null && tcpClient.Connected)
            {
                byte[] data = Encoding.UTF8.GetBytes(msg);
                try { await stream.WriteAsync(data, 0, data.Length); }
                catch
                {
                    Debug.LogError("[VoiceChat] Failed to send TCP message. Will reconnect...");
                    Disconnect();
                    StartReconnectLoop();
                }
            }
            else if (!reconnecting)
            {
                StartReconnectLoop();
            }
        }

        /// <summary>
        /// sends periodic heartbeat messages to keep connection alive
        /// </summary>
        private static async Task HeartbeatLoop(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                await Task.Delay(heartbeatInterval, token).ContinueWith(_ => { });
                if (token.IsCancellationRequested) break;

                await SendMessage("HEARTBEAT");
            }
        }

        /// <summary>
        /// continuously reads messages from tcp stream
        /// handles disconnects and reconnects automatically
        /// </summary>
        private static async Task ReceiveLoop(CancellationToken token)
        {
            byte[] buffer = new byte[4096];
            try
            {
                while (!token.IsCancellationRequested)
                {
                    int read = await stream.ReadAsync(buffer, 0, buffer.Length, token);
                    if (read == 0) break;

                    string message = Encoding.UTF8.GetString(buffer, 0, read);
                    HandleServerMessage(message);
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[VoiceChat] ReceiveLoop error: " + ex.Message);
            }
            finally
            {
                Debug.LogWarning("[VoiceChat] TCP connection lost. Disconnecting...");
                Disconnect();
                StartReconnectLoop();
            }
        }

        /// <summary>
        /// parses messages from server
        /// updates client list or triggers playback for speaking clients
        /// </summary>
        private static void HandleServerMessage(string message)
        {
            lock (msgLock) { lastMessage = message; }

            if (message.StartsWith("CLIENTS"))
            {
                ClientList.Clear();
                string[] parts = message.Split('|');
                if (parts.Length > 1)
                {
                    foreach (var c in parts[1].Split(','))
                    {
                        var kv = c.Split(':');
                        if (kv.Length == 2) ClientList[kv[0]] = kv[1];
                    }
                }
                Debug.Log("[VoiceChat] Client list updated: " + ClientList.Count);
            }
            else if (message.StartsWith("SPEAKING|"))
            {
                string[] parts = message.Split('|');
                if (parts.Length == 5)
                {
                    string scene = parts[1];
                    string name = parts[2];
                    string steamId = parts[3];
                    Debug.Log($"[VoiceChat] SPEAKING: {name} ({steamId}) in scene {scene}");

                    if (SceneManager.GetActiveScene().name == scene)
                        VoiceManager.AllowScenePlayback(scene, name);
                }
            }
        }

        /// <summary>
        /// waits until a specific message appears or timeout elapses
        /// </summary>
        public static async Task<bool> WaitForResponse(string expected, int timeoutMs)
        {
            int waited = 0;
            while (waited < timeoutMs)
            {
                string msg;
                lock (msgLock) { msg = lastMessage; }
                if (msg.Contains(expected)) return true;

                await Task.Delay(100);
                waited += 100;
            }
            return false;
        }

        /// <summary>
        /// stops connection, cancels loops and disposes tcp client and stream
        /// </summary>
        public static void Disconnect()
        {
            try
            {
                State = ConnectionState.Disconnected;
                cts?.Cancel();
                stream?.Close();
                tcpClient?.Close();

                tcpClient = null;
                stream = null;
                cts = null;
            }
            catch { }
        }

        /// <summary>
        /// cancels any existing tasks and closes streams
        /// prepares for a fresh connection
        /// </summary>
        private static void Cleanup()
        {
            try
            {
                cts?.Cancel();
                monitorCts?.Cancel();
                stream?.Close();
                tcpClient?.Close();

                tcpClient = null;
                stream = null;
                cts = null;
            }
            catch { }
        }
    }
}
