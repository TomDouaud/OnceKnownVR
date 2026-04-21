using UnityEngine;
using UnityEngine.Networking;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;

// ════════════════════════════════════════════════════════════════════════════
//  LLMService  (Singleton)
//
//  Streaming LLM chat service.
//  Receives a transcription + emotion, POSTs to the LLM endpoint,
//  and streams SSE tokens back via events.
//
//  History is owned internally — callers just call Send(text, emotion).
//  Trimming strategy: keep at most MAX_HISTORY_TURNS user/assistant pairs,
//  and remove oldest pairs when total chars exceed MAX_HISTORY_CHARS.
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

    // ── History settings ───────────────────────────────────────────────────
    private const int MAX_HISTORY_TURNS = 30;
    private const int MAX_HISTORY_CHARS = 8000;

    // ── Events ─────────────────────────────────────────────────────────────
    /// <summary>Fired for each streamed token.</summary>
    public event EventHandler<LLMTokenEventArgs> OnTokenReceived;

    /// <summary>Fired once the full response has been assembled.</summary>
    public event EventHandler<LLMCompleteEventArgs> OnResponseComplete;

    // ── State ──────────────────────────────────────────────────────────────
    public bool IsBusy { get; private set; }

    private bool                 _cancelled     = false;
    private UnityWebRequest      _activeRequest = null;
    private List<HistoryMessage> _history       = new List<HistoryMessage>();
    private string               _pendingPrompt = null;

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

    /// Abort any in-flight request and reset state.
    /// History is NOT cleared — a cancelled turn is simply not committed.
    public void Cancel()
    {
        if (!IsBusy) return;
        _cancelled     = true;
        _activeRequest?.Abort();
        _activeRequest = null;
        _pendingPrompt = null;
        IsBusy         = false;
        Debug.Log("[LLM] Cancelled.");
    }

    /// Wipe the full conversation history (e.g. new visitor / scene reload).
    public void ClearHistory()
    {
        _history.Clear();
        Debug.Log("[LLM] History cleared.");
    }

    /// Fire the streaming LLM request.
    public void Send(string transcription, string emotion, string responsePrefix = null)
    {
        if (IsBusy)
        {
            Debug.LogWarning("[LLM] Already processing a request — ignoring.");
            return;
        }
        
        StartCoroutine(StreamRequest(transcription, emotion, responsePrefix));
    }

    // ════════════════════════════════════════════════════════════════════════
    //  History management
    // ════════════════════════════════════════════════════════════════════════

    private void CommitToHistory(string userPrompt, string assistantResponse)
    {
        _history.Add(new HistoryMessage { role = "user",      content = userPrompt });
        _history.Add(new HistoryMessage { role = "assistant", content = assistantResponse });
        TrimHistory();
        Debug.Log($"<color=grey>[LLM History] {_history.Count / 2} turn(s) in memory.</color>");
    }

    private void TrimHistory()
    {
        // Enforce turn cap — always remove in pairs to keep context coherent
        while (_history.Count > MAX_HISTORY_TURNS * 2)
        {
            _history.RemoveAt(0);
            _history.RemoveAt(0);
        }

        // Enforce character budget
        int totalChars = 0;
        foreach (var msg in _history) totalChars += msg.content.Length;

        while (totalChars > MAX_HISTORY_CHARS && _history.Count >= 2)
        {
            totalChars -= _history[0].content.Length + _history[1].content.Length;
            _history.RemoveAt(0);
            _history.RemoveAt(0);
        }
    }

    // ════════════════════════════════════════════════════════════════════════
    //  Network — streaming
    // ════════════════════════════════════════════════════════════════════════

    private IEnumerator StreamRequest(string transcription, string emotion, string responsePrefix = null)
    {
        IsBusy         = true;
        _cancelled     = false;
        _pendingPrompt = transcription;
        Debug.Log("<color=green>[LLM] Envoi au guide...</color>");

        string jsonBody = JsonUtility.ToJson(new LlmRequest
        {
            prompt         = transcription,
            emotion        = emotion,
            artifactId     = string.IsNullOrEmpty(currentArtifactId) ? null : currentArtifactId,
            stream         = true,
            responsePrefix = responsePrefix,
            history        = _history.Count > 0 ? _history : null
        });

        Debug.Log("<color=yellow>[LLM] Payload : </color>" + jsonBody);

        byte[] bodyRaw = Encoding.UTF8.GetBytes(jsonBody);

        using (UnityWebRequest request = new UnityWebRequest(llmUrl, "POST"))
        {
            _activeRequest = request;

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
                if (_cancelled) yield break;

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

            if (_cancelled) yield break;

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

                // Commit this exchange to history only on success
                if (!string.IsNullOrEmpty(_pendingPrompt) && !string.IsNullOrEmpty(fullResponse))
                    CommitToHistory(_pendingPrompt, fullResponse);

                OnResponseComplete?.Invoke(this,
                    new LLMCompleteEventArgs(fullResponse, success: true));
            }
            else
            {
                Debug.LogError($"Erreur LLM (Code {request.responseCode}) : {request.error}");
                OnResponseComplete?.Invoke(this,
                    new LLMCompleteEventArgs(null, success: false, error: request.error));
            }

            _activeRequest = null;
            _pendingPrompt = null;
            IsBusy         = false;
        }
    }
}