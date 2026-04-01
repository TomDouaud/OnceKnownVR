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
    
    public void BeginSession()
    {
        assembledText = "";
        sessionActive = true;
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
            // S'il n'y a pas de reste audio, on déclenche juste l'événement de fin
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

            if (request.result == UnityWebRequest.Result.Success)
            {
                string partialText = request.downloadHandler.text;
                
                // On ajoute un espace pour éviter de coller les mots entre les chunks
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
                // Même en cas d'erreur sur le dernier chunk, on libère le système
                if (isFinal) OnTranscriptionComplete?.Invoke(this, new STTChunkEventArgs(assembledText, true));
            }
        }
    }
}