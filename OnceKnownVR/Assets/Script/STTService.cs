using UnityEngine;
using UnityEngine.Networking;
using System;
using System.Collections;

public class STTService : MonoBehaviour
{
    // ── Singleton ──────────────────────────────────────────────────────────
    public static STTService Instance { get; private set; }

    // ── Configuration ─────────────────────────────────────────────────────
    [Header("STT Endpoint")]
    public string sttUrl    = "https://lordnns.myftp.org/api/ai/stt/transcribe";
    public string apiSecret = "Pure-Gallery-Silence-01-Ethereal!";

    // ── Events ─────────────────────────────────────────────────────────────
    public event EventHandler<STTChunkEventArgs> OnChunkResult;
    public event EventHandler<STTChunkEventArgs> OnTranscriptionComplete;

    // ── Internal state ─────────────────────────────────────────────────────
    private string assembledText = "";
    private bool   sessionActive = false;
    private bool   _cancelled    = false;

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

    /// Abort any in-flight STT requests and suppress completion events.
    public void Cancel()
    {
        _cancelled    = true;
        sessionActive = false;
        assembledText = "";
        StopAllCoroutines();
        Debug.Log("[STT] Cancelled.");
    }

    public void BeginSession()
    {
        assembledText = "";
        sessionActive = true;
        _cancelled    = false;
    }

    public void SendChunk(byte[] wavChunk)
    {
        if (!sessionActive || wavChunk == null) return;
        StartCoroutine(PostChunk(wavChunk, false));
    }

    public void FinalizeSession(byte[] finalWavChunk)
    {
        if (!sessionActive) return;
        sessionActive = false;

        if (finalWavChunk != null && finalWavChunk.Length > 0)
        {
            StartCoroutine(PostChunk(finalWavChunk, true));
        }
        else
        {
            OnTranscriptionComplete?.Invoke(this, new STTChunkEventArgs(assembledText, true));
        }
    }

    // ════════════════════════════════════════════════════════════════════════
    //  Network
    // ════════════════════════════════════════════════════════════════════════

    private IEnumerator PostChunk(byte[] audioData, bool isFinal)
    {
        WWWForm form = new WWWForm();
        form.AddBinaryData("file", audioData, "speech.wav", "audio/wav");

        using (UnityWebRequest request = UnityWebRequest.Post(sttUrl, form))
        {
            request.SetRequestHeader("x-vr-app-secret", apiSecret);
            yield return request.SendWebRequest();

            // Discard result if cancelled while the request was in-flight
            if (_cancelled) yield break;

            if (request.result == UnityWebRequest.Result.Success)
            {
                string partialText = request.downloadHandler.text;

                if (!string.IsNullOrEmpty(assembledText) && !string.IsNullOrEmpty(partialText))
                    assembledText += " ";

                assembledText += partialText;

                Debug.Log($"<color=cyan>[STT Chunk] </color>{partialText}");
                OnChunkResult?.Invoke(this, new STTChunkEventArgs(partialText, isFinal));

                if (isFinal)
                {
                    Debug.Log($"<color=green>[STT Complet] </color>{assembledText}");
                    OnTranscriptionComplete?.Invoke(this, new STTChunkEventArgs(assembledText, true));
                }
            }
            else
            {
                Debug.LogError($"Erreur STT (Chunk) : {request.error}");
                if (isFinal)
                    OnTranscriptionComplete?.Invoke(this, new STTChunkEventArgs(assembledText, true));
            }
        }
    }
}