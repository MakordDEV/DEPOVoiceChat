using CSCore;
using CSCore.CoreAudioAPI;
using CSCore.SoundIn;
using CSCore.SoundOut;
using CSCore.Streams;
using Steamworks;
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace DEPOVoiceChat
{
    /// <summary>
    /// manages microphone capture, playback, streaming via udp, and speaking indicators
    /// </summary>
    public static class VoiceManager
    {
        public static string[] MicDevices { get; private set; } = Array.Empty<string>();
        public static MMDevice[] CaptureDevices { get; private set; } = Array.Empty<MMDevice>();

        private static WasapiCapture capture;
        private static SoundInSource soundInSource;
        private static IWaveSource waveSource;
        private static WasapiOut playback;
        private static MMDeviceEnumerator deviceEnum;
        private static string allowedScene;
        private static readonly object lockObj = new object();
        public static readonly List<string> speaking = new List<string>();
        private static bool streaming = false;

        private static int udpReceivePort;
        private static Thread udpReceiveThread;
        private static bool receiving = false;
        private static UdpClient udpClient;
        private static IPEndPoint serverEndPoint;
        private static Thread keepAliveThreadSend;
        private static bool keepAliveSendRunning = false;
        private static WasapiOut playersPlayback;
        private static string instanceId;

        /// <summary>
        /// removes player from speaking list safely
        /// </summary>
        public static void RemoveFromSpeaking(string name)
        {
            lock (lockObj)
                speaking.Remove(name);
        }

        /// <summary>
        /// sends udp punch packets to server to open NAT mapping
        /// </summary>
        private static async void SendUdpPunch()
        {
            try
            {
                byte[] punch = { 0x00 };
                for (int i = 0; i < 3; i++)
                {
                    udpClient?.Send(punch, punch.Length, serverEndPoint);
                    await Task.Delay(50);
                }
                Debug.Log("[VoiceChat] UDP punch packets sent to server to open NAT mapping.");
            }
            catch (Exception e)
            {
                Debug.LogWarning("[VoiceChat] UDP punch failed: " + e.Message);
            }
        }

        /// <summary>
        /// starts background thread to keep udp connection alive
        /// </summary>
        private static void StartUdpKeepAliveSend()
        {
            if (keepAliveSendRunning) return;

            keepAliveSendRunning = true;
            keepAliveThreadSend = new Thread(async() =>
            {
                byte[] ka = { 0xFF };
                while (keepAliveSendRunning)
                {
                    try
                    {
                        udpClient?.Send(ka, ka.Length, serverEndPoint);
                    }
                    catch (Exception ex)
                    {
                        Debug.LogWarning("[VoiceChat] KeepAliveSend: " + ex.Message);
                    }
                    await Task.Delay(2000);
                }
            })
            { IsBackground = true };
            keepAliveThreadSend.Start();
        }

        /// <summary>
        /// scans available audio capture devices and fills lists
        /// </summary>
        public static void InitDevices()
        {
            try
            {
                deviceEnum = new MMDeviceEnumerator();
                var devs = deviceEnum.EnumAudioEndpoints(DataFlow.Capture, DeviceState.Active);
                var names = new List<string>();
                var mmDevices = new List<MMDevice>();

                foreach (var d in devs)
                {
                    names.Add(d.FriendlyName);
                    mmDevices.Add(d);
                }

                MicDevices = names.ToArray();
                CaptureDevices = mmDevices.ToArray();
            }
            catch (Exception e)
            {
                Debug.LogError("[VoiceChat] CSCore: InitDevices failed: " + e.Message);
                MicDevices = Array.Empty<string>();
                CaptureDevices = Array.Empty<MMDevice>();
            }
        }

        /// <summary>
        /// starts local microphone capture and self playback
        /// </summary>
        public static void StartCapture(int deviceIndex)
        {
            StopCapture();

            if (!SettingsManager.CurrentSettings.hearSelf || CaptureDevices.Length == 0) return;
            if (deviceIndex < 0 || deviceIndex >= CaptureDevices.Length) return;

            try
            {
                var device = CaptureDevices[deviceIndex];
                capture = new WasapiCapture() { Device = device };
                capture.Initialize();

                soundInSource = new SoundInSource(capture) { FillWithZeros = true };
                waveSource = soundInSource.ToSampleSource().ToWaveSource(16);

                playback = new WasapiOut();
                playback.Initialize(waveSource);
                playback.Volume = SettingsManager.CurrentSettings.selfVolume;

                capture.Start();
                playback.Play();
                Debug.Log("[VoiceChat] VoiceManager: Started capture on " + device.FriendlyName);
            }
            catch (Exception e)
            {
                Debug.LogError("[VoiceChat] VoiceManager: StartCapture failed: " + e.Message);
                StopCapture();
            }
        }

        /// <summary>
        /// stops and disposes microphone capture and playback
        /// </summary>
        public static void StopCapture()
        {
            try { capture?.Stop(); } catch { }
            try { playback?.Stop(); } catch { }
            try { capture?.Dispose(); } catch { }
            try { playback?.Dispose(); } catch { }
            try { soundInSource?.Dispose(); } catch { }
            try { waveSource?.Dispose(); } catch { }

            capture = null;
            playback = null;
            soundInSource = null;
            waveSource = null;
        }

        /// <summary>
        /// sets unique instance id for this voice client
        /// </summary>
        public static void SetInstanceId(string instanceid) => instanceId = instanceid;

        /// <summary>
        /// starts sending microphone audio via udp
        /// handles reconnection and punch packets
        /// </summary>
        public static async void StartVoiceStream()
        {
            StopVoiceStream();
            streaming = true;

            try
            {
                if (NetworkManager.State != NetworkManager.ConnectionState.Connected)
                {
                    Debug.LogWarning("[VoiceChat] TCP connection missing, trying reconnect...");
                    bool reconnected = await NetworkManager.Connect();
                    if (!reconnected)
                    {
                        Debug.LogError("[VoiceChat] failed to reconnect tcp.");
                        streaming = false;
                        return;
                    }
                }

                if (udpClient == null)
                {
                    udpClient = new UdpClient(0);
                    udpReceivePort = ((IPEndPoint)udpClient.Client.LocalEndPoint).Port;
                    udpClient.Client.ReceiveTimeout = 1000;
                    udpClient.Client.SendTimeout = 1000;
                    Debug.Log($"[VoiceChat] udp client created on {udpReceivePort}");
                }
                else
                {
                    udpReceivePort = ((IPEndPoint)udpClient.Client.LocalEndPoint).Port;
                    Debug.Log($"[VoiceChat] reusing udp client on {udpReceivePort}");
                }

                var addrs = Dns.GetHostAddresses("busiatep.ru");
                serverEndPoint = new IPEndPoint(addrs[0], 6001);

                StartUdpKeepAliveSend();
                SendUdpPunch();

                string localSteamID = SteamUser.GetSteamID().m_SteamID.ToString();
                await NetworkManager.SendMessage($"UDP_INFO|{localSteamID}|{udpReceivePort}|{instanceId}");
                Debug.Log($"[VoiceChat] sent udp info: {localSteamID}:{udpReceivePort}|{instanceId}");

                Thread sendThread = new Thread(SendAudioLoop) { IsBackground = true };
                sendThread.Start();

                Debug.Log("[VoiceChat] audio send thread started.");
            }
            catch (Exception ex)
            {
                Debug.LogError("[VoiceChat] error starting voice stream: " + ex);
                streaming = false;
            }
        }

        /// <summary>
        /// stops sending microphone audio
        /// </summary>
        public static void StopVoiceStream()
        {
            if (!streaming) return;
            streaming = false;

            keepAliveSendRunning = false;
            Task.Run(() => keepAliveThreadSend.Join());
            keepAliveThreadSend = null;

            Debug.Log("[VoiceChat] stream stopped.");
        }

        /// <summary>
        /// starts receiving audio via udp and playback
        /// </summary>
        public static async void StartReceiving()
        {
            if (receiving) return;

            try
            {
                if (udpClient == null)
                    udpClient = new UdpClient(0);

                udpReceivePort = ((IPEndPoint)udpClient.Client.LocalEndPoint).Port;
                receiving = true;

                udpClient.Client.ReceiveTimeout = 1000;
                udpClient.Client.SendTimeout = 1000;

                string steamID = SteamUser.GetSteamID().m_SteamID.ToString();
                await NetworkManager.SendMessage($"UDP_INFO|{steamID}|{udpReceivePort}|{instanceId}");

                SendUdpPunch();

                udpReceiveThread = new Thread(ReceiveAudioLoop) { IsBackground = true };
                udpReceiveThread.Start();

                Debug.Log($"[VoiceChat] udp audio receive started on port {udpReceivePort}");
            }
            catch (Exception ex)
            {
                Debug.LogError("[VoiceChat] failed to start udp receive: " + ex.Message);
            }
        }

        /// <summary>
        /// stops udp audio receiving
        /// </summary>
        public static void StopReceiving()
        {
            try
            {
                receiving = false;
                Task.Run(() => keepAliveThreadSend.Join());
                udpReceiveThread = null;
                Debug.Log("[VoiceChat] udp receive stopped.");
            }
            catch (Exception ex)
            {
                Debug.LogError("[VoiceChat] error stopping udp receive: " + ex.Message);
            }
        }

        /// <summary>
        /// main loop to process incoming audio
        /// writes to buffer and triggers speaking indicators
        /// </summary>
        private static async void ReceiveAudioLoop()
        {
            IPEndPoint remoteEP = new IPEndPoint(IPAddress.Any, 0);
            var format = new CSCore.WaveFormat(48000, 16, 1);
            int bufferSize = format.BytesPerSecond * SettingsManager.CurrentSettings.bufferSizeMs / 1000;
            var bufferSource = new WriteableBufferingSource(format, bufferSize) { FillWithZeros = true };

            try
            {
                playersPlayback = new WasapiOut();
                playersPlayback.Initialize(bufferSource);
                playersPlayback.Volume = SettingsManager.CurrentSettings.playersVolume;
                playersPlayback.Play();

                SendUdpPunch();

                while (receiving)
                {
                    byte[] data = null;
                    try
                    {
                        data = udpClient.Receive(ref remoteEP);
                    }
                    catch (SocketException se)
                    {
                        if (se.SocketErrorCode != SocketError.TimedOut)
                            Debug.LogWarning("[VoiceChat] ReceiveAudioLoop SocketException: " + se.Message);
                        continue;
                    }

                    if (data != null && data.Length > 0)
                    {
                        bufferSource.Write(data, 0, data.Length);

                        UnityMainThreadDispatcher.Enqueue(() =>
                        {
                            lock (lockObj)
                            {
                                foreach (string name in speaking)
                                    SpeakingIndicator.OnPlayerSpeaking(name);
                            }
                        });
                    }
                    await Task.Delay(1);
                }
            }
            catch (Exception ex)
            {
                Debug.LogError("[VoiceChat] ReceiveAudioLoop fatal: " + ex);
            }
            finally
            {
                try { playersPlayback?.Stop(); } catch { }
                try { playersPlayback?.Dispose(); } catch { }
            }
        }

        /// <summary>
        /// main loop to capture microphone audio and send via udp
        /// handles voice activation and buffer
        /// </summary>
        private static async void SendAudioLoop()
        {
            try
            {
                if (serverEndPoint == null)
                {
                    var addrs = Dns.GetHostAddresses("busiatep.ru");
                    serverEndPoint = new IPEndPoint(addrs[0], 6001);
                }

                using (var capture = new WasapiCapture())
                {
                    capture.Initialize();
                    using (var source = new SoundInSource(capture) { FillWithZeros = false })
                    {
                        var targetFormat = new CSCore.WaveFormat(48000, 16, 1);
                        var converted = source
                            .ToSampleSource()
                            .ToMono()
                            .ChangeSampleRate(targetFormat.SampleRate)
                            .ToWaveSource(16);

                        capture.Start();
                        byte[] buffer = new byte[converted.WaveFormat.BytesPerSecond * SettingsManager.CurrentSettings.bufferSizeMs / 1000];

                        while (streaming)
                        {
                            int read = converted.Read(buffer, 0, buffer.Length);
                            bool sendData = true;

                            if (SettingsManager.CurrentSettings.micMode == MicMode.VoiceActivation)
                            {
                                float rms = 0f;
                                for (int i = 0; i < read; i += 2)
                                {
                                    short sample = (short)(buffer[i] | (buffer[i + 1] << 8));
                                    float f = sample / 32768f;
                                    rms += f * f;
                                }
                                if (read > 0)
                                    rms = Mathf.Sqrt(rms / (read / 2));

                                float db = 20f * Mathf.Log10(Mathf.Max(rms, 0.0001f));
                                sendData = db >= SettingsManager.CurrentSettings.voiceThresholdDb;
                            }

                            if (sendData && read > 0)
                                udpClient?.Send(buffer, read, serverEndPoint);

                            await Task.Delay(10);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogError("[VoiceChat] udp capture error: " + ex.Message);
            }
        }

        /// <summary>
        /// stops udp receiving and restarts it
        /// </summary>
        public static void RestartUdp()
        {
            StopReceiving();

            keepAliveSendRunning = false;
            Task.Run(() => keepAliveThreadSend.Join());
            keepAliveThreadSend = null;

            try { udpClient?.Close(); } catch { }
            udpClient = null;

            receiving = false;

            StartReceiving();
        }

        /// <summary>
        /// updates volume of all other players
        /// </summary>
        public static void UpdatePlayersVolume(float volume)
        {
            if (playersPlayback != null)
                playersPlayback.Volume = volume;
        }

        /// <summary>
        /// updates self playback volume
        /// </summary>
        public static void UpdateSelfVolume(float volume)
        {
            if (playback != null)
                playback.Volume = volume;
        }

        /// <summary>
        /// allows playback for a specific scene and adds player to speaking list
        /// </summary>
        public static void AllowScenePlayback(string scene, string name)
        {
            lock (lockObj)
            {
                allowedScene = scene;
                if (!speaking.Contains(name))
                    speaking.Add(name);
            }
        }

        /// <summary>
        /// checks if playback is allowed for the current scene
        /// </summary>
        public static bool IsAllowed(string scene)
        {
            return allowedScene != null && allowedScene == scene;
        }
    }
}
