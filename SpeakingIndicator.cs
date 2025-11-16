using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

namespace DEPOVoiceChat
{
    /// <summary>
    /// handles showing a speaking indicator for players with detailed debug logs
    /// </summary>
    public class SpeakingIndicator : MonoBehaviour
    {
        public static readonly Dictionary<string, float> speakingPlayers = new Dictionary<string, float>();
        public static readonly Dictionary<string, Coroutine> activeCoroutines = new Dictionary<string, Coroutine>();
        public static readonly object lockObj = new object();

        public static string oldSpeakText;
        public static string speakText = $"[{Localization.T("speaking")}]";
        private static readonly float silenceTimeout = 1f;

        /// <summary>
        /// updates all speaking indicators and removes timed out players
        /// </summary>
        public static void UpdateSpeakingIndicators()
        {
            float currentTime = Time.time;
            List<string> toRemove = new List<string>();

            if (speakText != $"[{Localization.T("speaking")}]") 
                UpdateSpeakText();

            lock (lockObj)
            {
                foreach (var kvp in speakingPlayers)
                {
                    if (currentTime - kvp.Value > silenceTimeout)
                    {
                        toRemove.Add(kvp.Key);
                    }
                }

                foreach (string name in toRemove)
                {
                    speakingPlayers.Remove(name);
                }
            }

            foreach (string name in toRemove)
            {
                RemoveIndicator(name);
            }
        }

        /// <summary>
        /// updates the speaking text after language change
        /// </summary>
        public static void UpdateSpeakText()
        {
            oldSpeakText = speakText;
            speakText = $"[{Localization.T("speaking")}]";

            UnityMainThreadDispatcher.Enqueue(() =>
            {
                Text[] texts = GameObject.FindObjectsOfType<Text>(true);

                foreach (var t in texts)
                {
                    if (!string.IsNullOrEmpty(oldSpeakText) && t.text.Contains(oldSpeakText))
                    {
                        t.text = t.text.Replace(oldSpeakText, speakText);
                    }
                }
            });
        }

        /// <summary>
        /// handles a player starting to speak using dispatcher 
        /// </summary>
        public static void OnPlayerSpeaking(string name)
        {
            if (string.IsNullOrEmpty(name))
            {
                Debug.LogWarning("[SpeakingIndicator] OnPlayerSpeaking called with null or empty name");
                return;
            }

            bool startCoroutine = false;

            lock (lockObj)
            {
                speakingPlayers[name] = Time.time;
                if (!activeCoroutines.ContainsKey(name))
                {
                    startCoroutine = true;
                }
            }

            if (startCoroutine)
            {
                UnityMainThreadDispatcher.Enqueue(() =>
                {
                    Coroutine c = DispatcherAddIndicatorCoroutine(name);
                    lock (lockObj)
                    {
                        activeCoroutines[name] = c;
                    }
                });
            }
        }

        /// <summary>
        /// starts add indicator coroutine via dispatcher
        /// </summary>
        private static Coroutine DispatcherAddIndicatorCoroutine(string playerName)
        {
            var dummyObj = new GameObject($"DispatcherCoroutine_{playerName}");
            var runner = dummyObj.AddComponent<DispatcherCoroutineRunner>();
            return runner.StartCoroutine(runner.AddIndicatorCoroutine(playerName));
        }

        /// <summary>
        /// removes indicator using dispatcher safely
        /// </summary>
        private static void RemoveIndicator(string playerName)
        {
            UnityMainThreadDispatcher.Enqueue(() =>
            {
                var dummyObj = new GameObject($"DispatcherRemove_{playerName}");
                var runner = dummyObj.AddComponent<DispatcherCoroutineRunner>();
                runner.StartCoroutine(runner.RemoveIndicatorCoroutine(playerName));
            });
        }

        public static Text FindTextForPlayer(string playerName)
        {
            Text[] texts = GameObject.FindObjectsOfType<Text>(true);
            Text found = texts.FirstOrDefault(t => t.name.Contains("NameJugador") && t.text.Contains(playerName));
            if (found == null)
                Debug.LogWarning($"[SpeakingIndicator] FindTextForPlayer: Could not find Text for '{playerName}'");
            return found;
        }
    }
    /// <summary>
    /// helper component for running coroutines
    /// </summary>
    public class DispatcherCoroutineRunner : MonoBehaviour
    {
        public IEnumerator AddIndicatorCoroutine(string playerName)
        {
            Text playerText = SpeakingIndicator.FindTextForPlayer(playerName);
            if (playerText == null)
            {
                Debug.LogWarning($"[DispatcherCoroutineRunner] could not find Text for '{playerName}'");
                yield break;
            }

            if (!playerText.text.Contains(SpeakingIndicator.speakText))
            {
                playerText.text += $" {SpeakingIndicator.speakText}";
            }

            Outline outline = playerText.GetComponent<Outline>();
            if (outline != null)
            {
                outline.enabled = false;
                yield return new WaitForSeconds(0.01f);
                outline.enabled = true;
            }

            while (true)
            {
                bool stillSpeaking;
                lock (SpeakingIndicator.lockObj)
                {
                    stillSpeaking = SpeakingIndicator.speakingPlayers.ContainsKey(playerName);
                }

                if (!stillSpeaking)
                {
                    break;
                }

                yield return null;
            }

            if (playerText != null && playerText.text.Contains(SpeakingIndicator.speakText))
            {
                playerText.text = playerText.text.Replace($" {SpeakingIndicator.speakText}", "");
            }

            if (outline != null)
            {
                outline.enabled = false;
                yield return new WaitForSeconds(0.01f);
                outline.enabled = true;
            }

            lock (SpeakingIndicator.lockObj)
            {
                SpeakingIndicator.activeCoroutines.Remove(playerName);
            }

            VoiceManager.RemoveFromSpeaking(playerName);
            Destroy(gameObject);
        }

        public IEnumerator RemoveIndicatorCoroutine(string playerName)
        {
            Text playerText = SpeakingIndicator.FindTextForPlayer(playerName);
            if (playerText != null && playerText.text.Contains(SpeakingIndicator.speakText))
            {
                playerText.text = playerText.text.Replace($" {SpeakingIndicator.speakText}", "");
            }

            Outline outline = playerText?.GetComponent<Outline>();
            if (outline != null)
            {
                outline.enabled = false;
                yield return new WaitForSeconds(0.01f);
                outline.enabled = true;
            }

            lock (SpeakingIndicator.lockObj)
            {
                SpeakingIndicator.activeCoroutines.Remove(playerName);
            }

            VoiceManager.RemoveFromSpeaking(playerName);
            Destroy(gameObject);
        }
    }
}