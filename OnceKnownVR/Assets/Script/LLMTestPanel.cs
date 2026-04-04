using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class LLMTestPanel : MonoBehaviour
{
    [Header("UI")]
    public TMP_InputField promptInput;
    public Button         sendButton;

    void Start()
    {
        sendButton.onClick.AddListener(Send);
    }

    [ContextMenu("Send Test Prompt")]
    public void Send()
    {
        string prompt = promptInput != null ? promptInput.text.Trim() : "Parle moi de ce musée.";

        if (string.IsNullOrEmpty(prompt))
        {
            Debug.LogWarning("[LLMTest] Prompt is empty.");
            return;
        }

        if (LLMService.Instance == null || TTSService.Instance == null)
        {
            Debug.LogError("[LLMTest] Services not found.");
            return;
        }

        string filler = FillerBank.Pick();
        Debug.Log($"<color=orange>[LLMTest] Sending: \"{prompt}\" | Filler: \"{filler}\"</color>");

        TTSService.Instance.BeginSession();
        TTSService.Instance.FeedToken(filler);
        LLMService.Instance.Send(prompt, "Neutral", filler);
    }
}