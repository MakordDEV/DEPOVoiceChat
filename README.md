# DEPO Voice Chat Mod

![Downloads](https://img.shields.io/github/downloads/makorddev/DEPOVoiceChat/total?style=for-the-badge)
![License](https://img.shields.io/github/license/makorddev/DEPOVoiceChat?style=for-the-badge)
![Unity](https://img.shields.io/badge/Unity-2021%2B-black?logo=unity&style=for-the-badge)
![BepInEx](https://img.shields.io/badge/BepInEx-5.4%2B-blue?style=for-the-badge)
![CSCore](https://img.shields.io/badge/Audio-CSCore-orange?style=for-the-badge)

## What is this?

Voicechat mod for Unity game **DEPO: Death Epileptic Pixel Origins**. It lets players talk to each other in real time during the same session using my dedicated server.
Supports push-to-talk, voice activation, microphone selection with self-hearing, per-player volume, and speaking indicators.

---

## Features

- Live voice chat across all players in the session
- Adjustable microphone and player volume
- Push-to-talk and voice activation modes
- Microphone selection with real-time self-listening
- Speaking indicators (shows who's talking)
- Automatic reconnection and NAT traversal

---

## Requirements

- BepInEx 5.4+
- .NET Framework 4.7.2+
- DEPO: Death Epileptic Pixel Origins from Steam

---

## Installation

1. Install the game  
   Download DEPO from Steam: [https://store.steampowered.com/app/1091320/](https://store.steampowered.com/app/1091320/)

2. Install BepInEx
   Download the latest BepInEx 5 (x64) from [the official repo](https://github.com/BepInEx/BepInEx) and extract it into the game's root folder (next to `DEPO.exe`).

3. Install the mod  
   - Download the latest release from the [Releases](../../releases) page.
   - Place `CSCore.dll` and `CSCore.xml` into `BepInEx/core/`.
   - Place `DEPOVoiceChat.dll` into `BepInEx/plugins/` (create the folder if it doesn't exist).

4. Launch the game
   Start DEPO through Steam. The mod should load automatically via BepInEx.

---

## Status

Work on this project is temporarily suspended. I may return to finalizing it later.

---

## Contributing

Issues, feature suggestions, and pull requests are welcome. Feel free to reach out on Discord/X: **makordikrom**.

If you encounter crashes, please attach logs from:  
`%USERPROFILE%\AppData\LocalLow\6 Faces Team\DEPO\player.log`

---
