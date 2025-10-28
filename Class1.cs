using BepInEx;
using UnityEngine;

namespace DEPOVoiceChat
{
    [BepInPlugin("ru.mxyffel_makordikrom.depovoicechat", "Hello World Mod", "1.0.0")]
    public class HelloWorld : BaseUnityPlugin
    {
        void Awake()
        {
            Logger.LogInfo("Hello World! Мод загружен.");
            Debug.Log("Hello World! (Unity Debug)");
        }

        void Start()
        {
            Debug.Log("Hello World в Start!");
        }

        void Update()
        {
            if (Input.GetKeyDown(KeyCode.H))
            {
                Debug.Log("Нажата клавиша H — Hello World!");
            }
        }
    }
}
