using CSCore;
using CSCore.CoreAudioAPI;
using CSCore.SoundIn;
using CSCore.SoundOut;
using CSCore.Streams;
using Steamworks;
using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using UnityEngine;

namespace DEPOVoiceChat
{
    public static class VoiceManager
    {
        public static string[] MicDevices { get; private set; } = new string[0];
        public static MMDevice[] CaptureDevices { get; private set; } = new MMDevice[0];

        private static WasapiCapture capture;
        private static SoundInSource soundInSource;
        private static IWaveSource waveSource;
        private static WasapiOut playback;
        private static MMDeviceEnumerator deviceEnum;
        private static string allowedScene;
        private static readonly object lockObj = new object();
        private static bool streaming = false;

        private static int udpReceivePort;
        private static Thread udpReceiveThread;
        private static bool receiving = false;
        private static UdpClient udpClient;
        private static IPEndPoint serverEndPoint;
        private static string instanceId;

        public static void InitDevices()
        {
            try
            {
                deviceEnum = new MMDeviceEnumerator();
                var devs = deviceEnum.EnumAudioEndpoints(DataFlow.Capture, DeviceState.Active);
                var names = new System.Collections.Generic.List<string>();
                var mmDevices = new System.Collections.Generic.List<MMDevice>();

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
                MicDevices = new string[0];
                CaptureDevices = new MMDevice[0];
            }
        }

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

        public static void StopCapture()
        {
            try { capture?.Stop(); capture?.Dispose(); } catch { }
            try { playback?.Stop(); playback?.Dispose(); } catch { }
            try { soundInSource?.Dispose(); } catch { }
            try { waveSource?.Dispose(); } catch { }

            capture = null;
            playback = null;
            soundInSource = null;
            waveSource = null;
        }

        public static async void StartVoiceStream()
        {
            if (streaming) return;
            streaming = true;

            try
            {
                if (NetworkManager.State != NetworkManager.ConnectionState.Connected)
                {
                    Debug.LogWarning("[VoiceChat] TCP-соединение отсутствует, попытка реконнекта...");
                    bool reconnected = await NetworkManager.Connect();
                    if (!reconnected)
                    {
                        Debug.LogError("[VoiceChat] Не удалось восстановить TCP-соединение.");
                        streaming = false;
                        return;
                    }
                }

                if (udpClient == null)
                {
                    udpClient = new UdpClient(0);
                    udpReceivePort = ((IPEndPoint)udpClient.Client.LocalEndPoint).Port;
                    Debug.Log($"[VoiceChat] (StartVoiceStream) UDP клиент создан на {udpReceivePort}");
                }
                else
                {
                    udpReceivePort = ((IPEndPoint)udpClient.Client.LocalEndPoint).Port;
                    Debug.Log($"[VoiceChat] (StartVoiceStream) Reusing UDP client on {udpReceivePort}");
                }

                var addrs = Dns.GetHostAddresses("busiatep.ru");
                serverEndPoint = new IPEndPoint(addrs[0], 6001);

                await NetworkManager.SendMessage("UDP_REQUEST");

                bool ok = await NetworkManager.WaitForResponse("UDP_OK", 3000);
                if (!ok)
                {
                    Debug.LogWarning("[VoiceChat] Сервер не ответил на UDP_REQUEST, отмена запуска микрофона.");
                    streaming = false;
                    return;
                }

                string localSteamID = SteamUser.GetSteamID().m_SteamID.ToString();
                await NetworkManager.SendMessage($"UDP_INFO|{localSteamID}|{udpReceivePort}|{instanceId}");
                Debug.Log($"[VoiceChat] Отправлена информация о UDP: {localSteamID}:{udpReceivePort}|{instanceId}");

                Thread sendThread = new Thread(SendAudioLoop) { IsBackground = true };
                sendThread.Start();

                Debug.Log("[VoiceChat] Поток передачи звука запущен.");
            }
            catch (Exception ex)
            {
                Debug.LogError("[VoiceChat] Ошибка при запуске VoiceStream: " + ex);
                streaming = false;
            }
        }

        public static void StopVoiceStream()
        {
            if (!streaming) return;
            streaming = false;
            Debug.Log("[VoiceChat] Стрим остановлен.");
        }

        public static void StartReceiving(string instanceid_)
        {
            if (receiving) return;

            instanceId = instanceid_;

            try
            {
                if (udpClient == null)
                {
                    udpClient = new UdpClient(0);
                }
                udpReceivePort = ((IPEndPoint)udpClient.Client.LocalEndPoint).Port;
                receiving = true;

                string steamID = SteamUser.GetSteamID().m_SteamID.ToString();
                _ = NetworkManager.SendMessage($"UDP_INFO|{steamID}|{udpReceivePort}|{instanceId}");

                udpReceiveThread = new Thread(ReceiveAudioLoop);
                udpReceiveThread.IsBackground = true;
                udpReceiveThread.Start();

                Debug.Log($"[VoiceChat] UDP приём звука запущен на порту {udpReceivePort}");
            }
            catch (Exception ex)
            {
                Debug.LogError("[VoiceChat] Не удалось запустить UDP приём: " + ex.Message);
            }
        }

        public static void StopReceiving()
        {
            try
            {
                receiving = false;
                try { udpClient?.Close(); } catch { }
                udpClient = null;

                udpReceiveThread?.Join(500);
                udpReceiveThread = null;
                Debug.Log("[VoiceChat] UDP приём звука остановлен.");
            }
            catch (Exception ex)
            {
                Debug.LogError("[VoiceChat] Ошибка при остановке UDP приёма: " + ex.Message);
            }
        }

        private static void ReceiveAudioLoop()
        {
            try
            {
                IPEndPoint remoteEP = new IPEndPoint(IPAddress.Any, 0);
                var format = new CSCore.WaveFormat(48000, 16, 1);
                var bufferSource = new CSCore.Streams.WriteableBufferingSource(format)
                {
                    FillWithZeros = true
                };

                using (var playback = new CSCore.SoundOut.WasapiOut())
                {
                    playback.Initialize(bufferSource);
                    playback.Volume = SettingsManager.CurrentSettings.playersVolume;
                    playback.Play();

                    Debug.Log("[VoiceChat] Ожидание UDP-пакетов...");

                    while (receiving)
                    {
                        try
                        {
                            if (udpClient == null)
                            {
                                Thread.Sleep(50);
                                continue;
                            }

                            byte[] data = udpClient.Receive(ref remoteEP);
                            if (data != null && data.Length > 0)
                            {
                                int bytesPerSample = format.BitsPerSample / 8;
                                int len = (data.Length / bytesPerSample) * bytesPerSample;
                                if (len > 0)
                                    bufferSource.Write(data, 0, len);
                            }
                        }
                        catch (SocketException se)
                        {
                            if (receiving)
                                Debug.LogWarning("[VoiceChat] ReceiveAudioLoop SocketException: " + se.Message);
                        }
                        catch (Exception ex)
                        {
                            Debug.LogError("[VoiceChat] ReceiveAudioLoop: " + ex.Message);
                        }

                        Thread.Sleep(1);
                    }

                    playback.Stop();
                }
            }
            catch (Exception ex)
            {
                Debug.LogError("[VoiceChat] ReceiveAudioLoop fatal: " + ex.Message);
            }
        }

        private static void SendAudioLoop()
        {
            try
            {
                if (serverEndPoint == null)
                {
                    var addrs = Dns.GetHostAddresses("busiatep.ru");
                    serverEndPoint = new IPEndPoint(addrs[0], 6001);
                }

                var targetFormat = new CSCore.WaveFormat(48000, 16, 1);

                using (var capture = new CSCore.SoundIn.WasapiCapture(false, CSCore.CoreAudioAPI.AudioClientShareMode.Shared, 100))
                {
                    capture.Initialize();
                    var source = new CSCore.Streams.SoundInSource(capture) { FillWithZeros = false };

                    var converted = source
                        .ChangeSampleRate(targetFormat.SampleRate)
                        .ToSampleSource()
                        .ToMono()
                        .ToWaveSource(16);

                    capture.Start();
                    Debug.Log("[VoiceChat] Захват микрофона и передача по UDP начались.");

                    byte[] buffer = new byte[targetFormat.BytesPerSecond / 10];
                    int read;

                    while (streaming)
                    {
                        read = converted.Read(buffer, 0, buffer.Length);

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
                            rms = Mathf.Sqrt(rms / (read / 2));
                            float db = 20f * Mathf.Log10(Mathf.Max(rms, 0.0001f));

                            sendData = db >= SettingsManager.CurrentSettings.voiceThresholdDb;
                        }

                        if (sendData && read > 0 && udpClient != null)
                            udpClient.Send(buffer, read, serverEndPoint);

                        Thread.Sleep(10);
                    }

                    capture.Stop();
                }
            }
            catch (Exception ex)
            {
                Debug.LogError("[VoiceChat] Ошибка UDP-захвата: " + ex.Message);
            }
        }

        public static void AllowScenePlayback(string scene)
        {
            lock (lockObj)
            {
                allowedScene = scene;
            }
        }

        public static bool IsAllowed(string scene)
        {
            return allowedScene != null && allowedScene == scene;
        }
    }
}
