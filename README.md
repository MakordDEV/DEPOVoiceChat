# 🎙️ DEPO Voiceсhat Mod

![GitHub Release](https://img.shields.io/github/v/release/makorddev/DEPOVoiceChat?style=for-the-badge)
![Downloads](https://img.shields.io/github/downloads/makorddev/DEPOVoiceChat/total?style=for-the-badge)
![License](https://img.shields.io/github/license/makorddev/DEPOVoiceChat?style=for-the-badge)
![Unity](https://img.shields.io/badge/Unity-2021%2B-black?logo=unity&style=for-the-badge)
![BepInEx](https://img.shields.io/badge/BepInEx-5.4%2B-blue?style=for-the-badge)
![CSCore](https://img.shields.io/badge/Audio-CSCore-orange?style=for-the-badge)

## What is this?

A real-time voice communication mod for the Unity-based game **DEPO: Death Epileptic Pixel Origins**, enabling players to talk to each other in-game.  
It supports push-to-talk, voice activation, microphone selection, and player volume management.

---

## 🔧 Features

- 🎤 **Live voice chat** between all players in the same session  
- 🔊 **Adjustable microphone and player volume**  
- 🗣 **Push-to-talk and voice activation modes**  
- 🛠 **Microphone selection** with real-time self-listening  
- 🧾 **Speaking indicators** showing who is currently talking  
- 🛡️ Built using **CSCore**, **Unity**, **Harmony** and **BepInEx**

---

## 🛠 Requirements

1. **BepInEx** 5.4+  
2. **.NET Framework** 4.7+  
3. **DEPO: Death Epileptic Pixel Origins** 

---

## 🛠️ How to Install

1. **Install DEPO: Death Epileptic Pixel Origins**  
   Download and install the game from Steam:  
   https://store.steampowered.com/app/1091320/

2. **Install BepInEx**  
   Download the latest version of **BepInEx (x64)** from the official repository:  
   https://github.com/BepInEx/BepInEx  
   Then, extract the contents into the **root folder** of the game (where the `.exe` file is located).

3. **Install the Mod Plugin**  
   - Download the `DEPOVoiceChat.dll`, `CSCore.dll` and `CSCore.xml` file from the [Releases](../../releases) section of this GitHub repository.
   - Move the `CSCore.dll` and `CSCore.xml` file to the `BepInEx/core` folder inside the game directory.  
   - Move the `DEPOVoiceChat.dll` file to the `BepInEx/plugins` folder inside the game directory.  
     > 🔸 If the `plugins` folder does not exist, create it manually.
  

4. **Launch the Game**  
   Run DEPO through Steam as usual. If everything is installed correctly, the mod will be loaded automatically by BepInEx.

> If the `plugins` folder does not exist — create it manually.

### 4. Launch the Game  
Start DEPO through Steam.  
If the installation was successful, BepInEx will automatically load the mod.

---

## 🧪 Status

**Work in progress** — active development continues.

Planned improvements:

-  Player groups & localized communication  
-  Lower latency & optimized jitter buffering  
-  Noise suppression & voice filters  
-  UI improvements and localized menus  

---

## ❤️ Contributing

Issues, suggestions, and pull requests are welcome!

---

## ❓ FAQ (Frequently Asked Questions)


### **Q: Why does the game crash after installing the mod?**  
**A:** This may happen due to force-closing the game with Alt+F4 or changing device settings while the game is running. You can send the logs from  
``C:\Users\<USERNAME>\AppData\LocalLow\6 Faces Team\DEPO\player.log``  
to a GitHub Issue or directly to me on Discord/X: `makordikrom`.


### **Q: Why do commit names sometimes seem meaningless?**  
**A:** Occasionally, during minor or quick commits, I might use random words as commit names. In releases and important commits, I always provide meaningful comments.


### **Q: How to open voicechat menu?**  
**A:** Press LCtrl in the game, then press Q or another assigned key. You can keep voicechat menu open without opening online interaction menu, even if you don’t interact with it. However, if you want to interact with voicechat menu, the online interaction menu or the pause menu must be open.


### **Q: Can I completely disable voice chat?**  
**A:** Yes. In the mod settings, you can turn off both voice transmission and receiving audio from other players.


### **Q: Why voicechat does not work and menu does not open?**  
**A:** This may occur if `CSCore.dll` is not correctly placed in the `BepInEx/core` folder or if your system does not grant microphone permissions. Also, make sure the correct audio device is selected in the mod settings.


### **Q: Is voice activation based on microphone volume planned?**  
**A:** Yes. We already have a prototype for volume threshold, but the feature is not fully completed and requires further development.


### **Q: Can I adjust the volume of individual players separately?**  
**A:** Not yet. Sliders for individual player volume are planned for a future update.


### **Q: Is using the mod allowed by the game or Steam rules?**  
**A:** Yes. The mod does not modify game files and operates within the official BepInEx ecosystem, so it does not conflict with Steam rules.


### **Q: Is noise suppression or improved voice quality planned?**  
**A:** Yes. This is one of the key goals for upcoming updates.


### **Q: Will it be possible to talk only to nearby players (spatial audio)?**  
**A:** This feature is under consideration. Future versions may include group channels or radius-based voice chat.


### **Q: Is Linux or Proton supported?**  
**A:** All systems and computers that can run Steam, DEPO, and BepInEx are supported.

