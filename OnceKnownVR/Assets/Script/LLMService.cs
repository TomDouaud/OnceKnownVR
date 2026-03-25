using UnityEngine;
using UnityEngine.Networking;
using System;
using System.Collections;
using System.Text;

// ════════════════════════════════════════════════════════════════════════════
//  LLMService  (Singleton)
//
//  Streaming LLM chat service.
//  Receives a transcription + emotion, POSTs to the LLM endpoint,
//  and streams SSE tokens back via events.
//
// ════════════════════════════════════════════════════════════════════════════

public class LLMService : MonoBehaviour
{
    // ── Singleton ──────────────────────────────────────────────────────────
    public static LLMService Instance { get; private set; }

    // ── Configuration ─────────────────────────────────────────────────────
    [Header("LLM Endpoint")]
    public string llmUrl    = "https://lordnns.myftp.org/api/ai/llm/chat";
    public string apiSecret = "Pure-Gallery-Silence-01-Ethereal!";

    [Header("Contexte Musée")]
    public string currentArtifactId = "";

    // ── Events ─────────────────────────────────────────────────────────────
    /// <summary>Fired for each streamed token.</summary>
    public event EventHandler<LLMTokenEventArgs> OnTokenReceived;

    /// <summary>Fired once the full response has been assembled.</summary>
    public event EventHandler<LLMCompleteEventArgs> OnResponseComplete;

    // ── State ──────────────────────────────────────────────────────────────
    public bool IsBusy { get; private set; }

    // ════════════════════════════════════════════════════════════════════════
    //  Lifecycle
    // ════════════════════════════════════════════════════════════════════════

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    // ════════════════════════════════════════════════════════════════════════
    //  Public API
    // ════════════════════════════════════════════════════════════════════════
    
    /// Fire the streaming LLM request.
    public void Send(string transcription, string emotion)
    {
        if (IsBusy)
        {
            Debug.LogWarning("[LLM] Already processing a request — ignoring.");
            return;
        }
        StartCoroutine(StreamRequest(transcription, emotion));
    }

    // ════════════════════════════════════════════════════════════════════════
    //  Network — streaming
    // ════════════════════════════════════════════════════════════════════════

    private IEnumerator StreamRequest(string transcription, string emotion)
    {
        IsBusy = true;
        Debug.Log("<color=green>[LLM] Envoi au guide...</color>");

        string jsonBody = JsonUtility.ToJson(new LlmRequest
        {
            prompt     = transcription,
            emotion    = emotion,
            artifactId = string.IsNullOrEmpty(currentArtifactId) ? null : currentArtifactId,
            stream     = true
        });

        Debug.Log("<color=yellow>[LLM] Payload : </color>" + jsonBody);

        byte[] bodyRaw = Encoding.UTF8.GetBytes(jsonBody);

        using (UnityWebRequest request = new UnityWebRequest(llmUrl, "POST"))
        {
            request.uploadHandler   = new UploadHandlerRaw(bodyRaw);
            request.downloadHandler = new StreamingDownloadHandler();
            request.SetRequestHeader("Content-Type",      "application/json");
            request.SetRequestHeader("x-vr-app-secret",   apiSecret);

            var op = request.SendWebRequest();
            StreamingDownloadHandler streamHandler =
                (StreamingDownloadHandler)request.downloadHandler;

            // Poll for new chunks while in-flight
            while (!op.isDone)
            {
                string chunk = streamHandler.ConsumeNewData();
                if (!string.IsNullOrEmpty(chunk))
                {
                    string parsed = StreamingDownloadHandler.ParseSSEChunk(chunk);
                    Debug.Log("<color=cyan>[LLM stream] </color>" + parsed);

                    if (!string.IsNullOrEmpty(parsed))
                        OnTokenReceived?.Invoke(this, new LLMTokenEventArgs(parsed));
                }
                yield return null;
            }

            // Grab remaining data
            string remaining = streamHandler.ConsumeNewData();
            if (!string.IsNullOrEmpty(remaining))
            {
                string parsed = StreamingDownloadHandler.ParseSSEChunk(remaining);
                Debug.Log("<color=cyan>[LLM stream] </color>" + parsed);

                if (!string.IsNullOrEmpty(parsed))
                    OnTokenReceived?.Invoke(this, new LLMTokenEventArgs(parsed));
            }

            if (request.result == UnityWebRequest.Result.Success)
            {
                string fullResponse = streamHandler.GetFullText();
                Debug.Log("<color=green>[LLM COMPLETE] </color>" + fullResponse);

                OnResponseComplete?.Invoke(this,
                    new LLMCompleteEventArgs(fullResponse, success: true));
            }
            else
            {
                Debug.LogError($"Erreur LLM (Code {request.responseCode}) : {request.error}");
                OnResponseComplete?.Invoke(this,
                    new LLMCompleteEventArgs(null, success: false, error: request.error));
            }

            IsBusy = false;
        }
    }
}
