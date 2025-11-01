using CSCore;
using CSCore.CoreAudioAPI;
using CSCore.SoundIn;
using CSCore.SoundOut;
using CSCore.Streams;
using System;
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
    }
}
