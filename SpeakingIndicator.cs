using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using System.Linq;

namespace DEPOVoiceChat
{
    /// <summary>
    /// temporarily handles showing a speaking indicator for players but methods do not work reliably yet
    /// </summary>
    public class SpeakingIndicator : MonoBehaviour
    {
        // stores the last speaking time of each player
        private static readonly Dictionary<string, float> speakingPlayers = new Dictionary<string, float>();
        // keeps track of coroutines currently running for indicators
        private static readonly Dictionary<string, Coroutine> activeCoroutines = new Dictionary<string, Coroutine>();
        // used for thread safety on dictionaries
        private static readonly object lockObj = new object();
        // defines how long until a player is considered silent
        private static float silenceTimeout = 1.0f;

        /// <summary>
        /// updates speaking indicators and removes expired ones, temporary logic might miss some updates
        /// </summary>
        void Update()
        {
            float currentTime = Time.time;
            List<string> toRemove = new List<string>();

            lock (lockObj)
            {
                foreach (var kvp in speakingPlayers)
                {
                    if (currentTime - kvp.Value > silenceTimeout)
                        toRemove.Add(kvp.Key);
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
        /// called externally when a player starts speaking, triggers indicator coroutine but may fail in some cases
        /// </summary>
        /// <param name="name">player name</param>
        public static void OnPlayerSpeaking(string name)
        {
            if (string.IsNullOrEmpty(name)) return;

            SpeakingIndicator instance = FindObjectOfType<SpeakingIndicator>();
            if (instance == null) return;

            bool startCoroutine = false;

            lock (lockObj)
            {
                speakingPlayers[name] = Time.time;
                if (!activeCoroutines.ContainsKey(name))
                    startCoroutine = true;
            }

            if (startCoroutine)
            {
                Coroutine c = instance.StartCoroutine(instance.AddIndicatorCoroutine(name));
                lock (lockObj)
                {
                    activeCoroutines[name] = c;
                }
            }
        }

        /// <summary>
        /// coroutine to add the speaking indicator, enables outline effect briefly, does not always sync properly
        /// </summary>
        /// <param name="playerName">player name</param>
        private IEnumerator AddIndicatorCoroutine(string playerName)
        {
            Text playerText = FindTextForPlayer(playerName);
            if (playerText == null) yield break;

            if (!playerText.text.Contains("🔊"))
                playerText.text += " 🔊";

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
                lock (lockObj)
                {
                    stillSpeaking = speakingPlayers.ContainsKey(playerName);
                }

                if (!stillSpeaking) break;
                yield return null;
            }

            if (playerText != null && playerText.text.Contains("🔊"))
                playerText.text = playerText.text.Replace(" 🔊", "");

            if (outline != null)
            {
                outline.enabled = false;
                yield return new WaitForSeconds(0.01f);
                outline.enabled = true;
            }

            lock (lockObj)
            {
                activeCoroutines.Remove(playerName);
            }

            VoiceManager.RemoveFromSpeaking(playerName);
        }

        /// <summary>
        /// starts coroutine to remove indicator, may not work if instance is missing
        /// </summary>
        /// <param name="playerName">player name</param>
        private static void RemoveIndicator(string playerName)
        {
            SpeakingIndicator instance = FindObjectOfType<SpeakingIndicator>();
            if (instance != null)
                instance.StartCoroutine(instance.RemoveIndicatorCoroutine(playerName));
        }

        /// <summary>
        /// coroutine that removes the speaking indicator visually, may briefly leave the icon
        /// </summary>
        /// <param name="playerName">player name</param>
        private IEnumerator RemoveIndicatorCoroutine(string playerName)
        {
            Text playerText = FindTextForPlayer(playerName);
            if (playerText != null && playerText.text.Contains("🔊"))
                playerText.text = playerText.text.Replace(" 🔊", "");

            Outline outline = playerText?.GetComponent<Outline>();
            if (outline != null)
            {
                outline.enabled = false;
                yield return new WaitForSeconds(0.01f);
                outline.enabled = true;
            }

            lock (lockObj)
            {
                activeCoroutines.Remove(playerName);
            }

            VoiceManager.RemoveFromSpeaking(playerName);
        }

        /// <summary>
        /// finds a Text component for a player, temporary logic may not locate all players
        /// </summary>
        /// <param name="playerName">player name</param>
        /// <returns>text component or null</returns>
        private static Text FindTextForPlayer(string playerName)
        {
            Text[] texts = GameObject.FindObjectsOfType<Text>(true);
            return texts.FirstOrDefault(t => t.name.Contains("NameJugador") && t.text.Contains(playerName));
        }
    }
}
