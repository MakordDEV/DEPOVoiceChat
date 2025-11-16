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
    /// improved with proper async/await, cancellation, and error handling
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

        private static readonly string serverIp = "busiatep.ru";
        private static readonly int serverPort = 6000;
        private static readonly int heartbeatInterval = 5000;

        private static string lastMessage = "";
        private static readonly object msgLock = new object();

        private static bool reconnecting = false;

        /// <summary>
        /// connects to tcp server and starts heartbeat and receive loops
        /// sends initial client info and waits for clients list response
        /// includes retry, exception handling, and cancellation support
        /// </summary>
        public static async Task<bool> Connect()
        {
            if (State == ConnectionState.Connecting || State == ConnectionState.Connected)
                return State == ConnectionState.Connected;

            State = ConnectionState.Connecting;
            Cleanup();
            cts = new CancellationTokenSource();
            CancellationToken token = cts.Token;

            try
            {
                tcpClient = new TcpClient();
                await tcpClient.ConnectAsync(serverIp, serverPort);
                stream = tcpClient.GetStream();
                Debug.Log("[VoiceChat] TCP connected.");

                string info = $"INFO|{Steamworks.SteamUser.GetSteamID().m_SteamID}|{Steamworks.SteamFriends.GetPersonaName()}|{SceneManager.GetActiveScene().name}";
                await SendMessage(info);

                bool ok = await WaitForResponse("CLIENTS|", 2000);
                if (!ok) Debug.LogWarning("[VoiceChat] CLIENTS response timeout.");

                _ = Task.Run(() => HeartbeatLoop(token), token);
                _ = Task.Run(() => ReceiveLoop(token), token);

                State = ConnectionState.Connected;
                StartMonitor();
                return true;
            }
            catch (Exception ex)
            {
                Debug.LogError("[VoiceChat] Connect error: " + ex);
                Disconnect();
                StartReconnectLoop();
                return false;
            }
        }

        /// <summary>
        /// continuously checks connection state and starts reconnect if needed
        /// safe cancellation and exception handling
        /// </summary>
        private static void StartMonitor()
        {
            monitorCts?.Cancel();
            monitorCts = new CancellationTokenSource();
            CancellationToken token = monitorCts.Token;

            _ = Task.Run(async () =>
            {
                while (!token.IsCancellationRequested)
                {
                    try
                    {
                        await Task.Delay(3000, token);
                        if (State != ConnectionState.Connected && !reconnecting)
                        {
                            Debug.LogWarning("[VoiceChat] Connection lost. Trying to reconnect...");
                            StartReconnectLoop();
                        }
                    }
                    catch (TaskCanceledException) { break; }
                    catch (Exception ex) { Debug.LogError("[VoiceChat] Monitor error: " + ex); }
                }
            }, token);
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
        /// includes exception handling and token safety
        /// </summary>
        public static async Task SendMessage(string msg)
        {
            if (tcpClient != null && tcpClient.Connected)
            {
                byte[] data = Encoding.UTF8.GetBytes(msg);
                try
                {
                    await stream.WriteAsync(data, 0, data.Length);
                }
                catch (Exception ex)
                {
                    Debug.LogError("[VoiceChat] Failed to send TCP message: " + ex);
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
        /// includes safe cancellation handling
        /// </summary>
        private static async Task HeartbeatLoop(CancellationToken token)
        {
            try
            {
                while (!token.IsCancellationRequested)
                {
                    await Task.Delay(heartbeatInterval, token).ContinueWith(_ => { });
                    if (token.IsCancellationRequested) break;

                    await SendMessage("HEARTBEAT");
                }
            }
            catch (TaskCanceledException) { }
            catch (Exception ex) { Debug.LogError("[VoiceChat] HeartbeatLoop error: " + ex); }
        }

        /// <summary>
        /// continuously reads messages from tcp stream
        /// handles disconnects and reconnects automatically
        /// logs errors and prevents unobserved exceptions
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
            catch (TaskCanceledException) { }
            catch (Exception ex)
            {
                Debug.LogWarning("[VoiceChat] ReceiveLoop error: " + ex);
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
        /// includes thread-safety and error handling
        /// </summary>
        private static void HandleServerMessage(string message)
        {
            lock (msgLock) { lastMessage = message; }

            try
            {
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
                }
                else if (message.StartsWith("SPEAKING|"))
                {
                    string[] parts = message.Split('|');
                    if (parts.Length == 4)
                    {
                        string scene = parts[1];
                        string name = parts[2];

                        if (SceneManager.GetActiveScene().name == scene)
                            VoiceManager.AllowScenePlayback(scene, name);
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[VoiceChat] HandleServerMessage error: " + ex);
            }
        }

        /// <summary>
        /// waits until a specific message appears or timeout elapses
        /// includes safe cancellation
        /// </summary>
        public static async Task<bool> WaitForResponse(string expected, int timeoutMs)
        {
            int waited = 0;
            while (waited < timeoutMs)
            {
                string msg;
                lock (msgLock) { msg = lastMessage; }
                if (msg.Contains(expected)) return true;

                await Task.Delay(10);
                waited += 10;
            }
            return false;
        }

        /// <summary>
        /// stops connection, cancels loops and disposes tcp client and stream
        /// safe for multiple calls
        /// </summary>
        public static void Disconnect()
        {
            try
            {
                State = ConnectionState.Disconnected;
                cts?.Cancel();
                monitorCts?.Cancel();
                stream?.Close();
                tcpClient?.Close();

                tcpClient = null;
                stream = null;
                cts = null;
                monitorCts = null;
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
                monitorCts = null;
            }
            catch { }
        }
    }
}
