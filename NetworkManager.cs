using System.Collections.Generic;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace DEPOVoiceChat
{
    public static class NetworkManager
    {
        public static Dictionary<string, string> ClientList { get; private set; } = new Dictionary<string, string>();
        private static TcpClient tcpClient;
        private static NetworkStream stream;
        private static CancellationTokenSource cts;
        private static string serverIp = "busiatep.ru";
        private static int serverPort = 6000;
        private static bool isConnecting = false;
        private static int heartbeatInterval = 5000;
        private static string lastMessage = "";
        private static readonly object msgLock = new object();

        public static void Connect()
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
                            Debug.Log("[VoiceChat] NetworkManager: TCP connected.");

                            try
                            {
                                string info = $"INFO|{Steamworks.SteamUser.GetSteamID().m_SteamID}|{Steamworks.SteamFriends.GetPersonaName()}|menus";
                                await SendMessage(info);
                            }
                            catch (System.Exception ex)
                            {
                                Debug.LogError("[VoiceChat] Failed to send INFO: " + ex.Message);
                            }

                            _ = Task.Run(() => HeartbeatLoop(cts.Token));
                            _ = Task.Run(() => ReceiveLoop(cts.Token));
                        }
                        catch (System.Exception ex) { Debug.LogError("[VoiceChat] TCP error: " + ex.Message); }
                    }
                    await Task.Delay(5000);
                }
            });
        }

        public static async Task SendMessage(string msg)
        {
            if (tcpClient != null && tcpClient.Connected)
            {
                byte[] data = Encoding.UTF8.GetBytes(msg);
                try { await stream.WriteAsync(data, 0, data.Length); }
                catch { Debug.LogError("[VoiceChat] Failed to send TCP message."); }
            }
        }

        private static async Task HeartbeatLoop(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                await Task.Delay(heartbeatInterval);
                await SendMessage("HEARTBEAT");
            }
        }

        private static async Task ReceiveLoop(CancellationToken token)
        {
            byte[] buffer = new byte[4096];
            while (!token.IsCancellationRequested)
            {
                if (stream != null && stream.DataAvailable)
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

        private static void HandleServerMessage(string message)
        {
            lock (msgLock)
            {
                lastMessage = message;
            }

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
        }

        public static async Task<bool> WaitForResponse(string expected, int timeoutMs)
        {
            int waited = 0;
            while (waited < timeoutMs)
            {
                string msg;
                lock (msgLock)
                {
                    msg = lastMessage;
                }
                if (msg.Contains(expected))
                    return true;

                await Task.Delay(100);
                waited += 100;
            }
            return false;
        }
        public static void Disconnect()
        {
            try
            {
                cts?.Cancel();
                stream?.Close();
                tcpClient?.Close();
                tcpClient = null;
                stream = null;
                cts = null;
                isConnecting = false;
            }
            catch { }
        }

    }
}
