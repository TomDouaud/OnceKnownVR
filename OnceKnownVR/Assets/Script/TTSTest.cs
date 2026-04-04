using UnityEngine;
using UnityEngine.InputSystem;

public class TTSTest : MonoBehaviour
{
    [Header("Test Sentence")]
    public string testSentence = "Bonjour, bienvenue au musée. Je suis ravi de vous accueillir.";

    [Header("Trigger")]
    public Key testKey = Key.T;

    void Update()
    {
        if (Keyboard.current != null && Keyboard.current[testKey].wasPressedThisFrame)
            RunTest();
    }

    [ContextMenu("Run TTS Test")]
    public void RunTest()
    {
        if (TTSService.Instance == null)
        {
            Debug.LogError("[TTSTest] TTSService not found.");
            return;
        }

        Debug.Log($"[TTSTest] Sending to phonemize + synth: \"{testSentence}\"");

        TTSService.Instance.BeginSession();
        
        TTSService.Instance.FeedToken(testSentence);
        TTSService.Instance.Complete();
    }
}