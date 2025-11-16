# 🚀 DEPO VoiceChat Mod

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
   👉 https://store.steampowered.com/app/1091320/

2. **Install BepInEx**  
   Download the latest version of **BepInEx (x64)** from the official repository:  
   👉 https://github.com/BepInEx/BepInEx  
   Then, extract the contents into the **root folder** of the game (where the `.exe` file is located).

3. **Install the Mod Plugin**  
   - Download the `DEPOVoiceChat.dll`, `CSCore.dll` and `CSCore.xml` file from the [Releases](../../releases) section of this GitHub repository.
   - Move the `CSCore.dll` and `CSCore.xml` file to the `BepInEx/core` folder inside the game directory.  
   - Move the `DEPOVoiceChat.dll` file to the `BepInEx/plugins` folder inside the game directory.  
     > 🔸 If the `plugins` folder does not exist, create it manually.
  

4. **Launch the Game**  
   Run DEPO through Steam as usual. If everything is installed correctly, the mod will be loaded automatically by BepInEx.

---

🧪 **Work in progress** – under active development, with planned features like groups for communication at different levels, improved latency, and noise suppression.
