using UnityEngine;
using UnityEngine.Networking;
using System;
using System.Collections;

// ════════════════════════════════════════════════════════════════════════════
//  STTService  (Singleton)
//
//  Speech-to-Text service.
//
//  CURRENT behaviour (unchanged from original):
//      After recording stops, receives the full WAV and sends it to the
//      server in a single POST.  Fires OnTranscriptionComplete.
//
//  FUTURE chunked-streaming layout:
//      While recording is live, the orchestrator calls SendChunk() at
//      regular intervals with partial audio buffers.  Each chunk fires
//      OnChunkResult with partial text.  When the orchestrator calls
//      FinalizeSession(), the last chunk is sent with a "final" flag
//      and OnTranscriptionComplete fires with the assembled full text.
//
//  The public surface is already shaped so that switching from
//  "single-shot" to "chunked" only requires filling in the stubbed
//  methods — no callers need to change.
// ════════════════════════════════════════════════════════════════════════════

public class STTService : MonoBehaviour
{
    // ── Singleton ──────────────────────────────────────────────────────────
    public static STTService Instance { get; private set; }

    // ── Configuration ─────────────────────────────────────────────────────
    [Header("STT Endpoint")]
    public string sttUrl    = "https://lordnns.myftp.org/api/ai/stt/transcribe";
    public string apiSecret = "Pure-Gallery-Silence-01-Ethereal!";

    // ── Events ─────────────────────────────────────────────────────────────
    /// <summary>Fired for every chunk result (partial or final).</summary>
    public event EventHandler<STTChunkEventArgs> OnChunkResult;

    /// <summary>Fired once after FinalizeSession — carries the full assembled transcription.</summary>
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
    
    /// Call when a new recording cycle begins.
    /// Resets internal accumulated text.
    public void BeginSession()
    {
        assembledText = "";
        sessionActive = true;
    }
    
    /// [FUTURE — CHUNKED STREAMING]
    /// Send a partial audio buffer while the user is still talking.
    /// Each call triggers OnChunkResult with partial text.
    /// Currently a no-op; fill in when the server supports streaming STT.
    public void SendChunk(byte[] wavChunk)
    {
        if (!sessionActive) return;

        // ── TODO: implement chunked upload ──
        // StartCoroutine(PostChunk(wavChunk, isFinal: false));
    }
    
    /// Call when recording stops.
    /// Sends the full WAV for transcription (current behaviour).
    /// In chunked mode this will send the last chunk with a "final" flag
    /// and assemble the complete text.
    public void FinalizeSession(byte[] fullWavData)
    {
        if (!sessionActive) return;
        sessionActive = false;

        // Current single-shot path
        StartCoroutine(PostFullAudio(fullWavData));
    }

    // ════════════════════════════════════════════════════════════════════════
    //  Network — single-shot
    // ════════════════════════════════════════════════════════════════════════

    private IEnumerator PostFullAudio(byte[] audioData)
    {
        WWWForm form = new WWWForm();
        form.AddBinaryData("file", audioData, "speech.wav", "audio/wav");

        Debug.Log("[STT] Envoi vers : " + sttUrl);
        using (UnityWebRequest request = UnityWebRequest.Post(sttUrl, form))
        {
            request.SetRequestHeader("x-vr-app-secret", apiSecret);
            yield return request.SendWebRequest();

            if (request.result == UnityWebRequest.Result.Success)
            {
                string transcription = request.downloadHandler.text;
                Debug.Log("<color=green>[STT] Transcription : </color>" + transcription);

                assembledText = transcription;

                OnChunkResult?.Invoke(this,
                    new STTChunkEventArgs(transcription, isFinal: true));

                OnTranscriptionComplete?.Invoke(this,
                    new STTChunkEventArgs(assembledText, isFinal: true));
            }
            else
            {
                Debug.LogError($"Erreur STT : {request.error}");
            }
        }
    }

    // ════════════════════════════════════════════════════════════════════════
    //  Network — chunked  (stub for future implementation)
    // ════════════════════════════════════════════════════════════════════════

    /*
    private IEnumerator PostChunk(byte[] wavChunk, bool isFinal)
    {
        // TODO:
        // 1. POST chunk to streaming STT endpoint
        // 2. Append partial text to assembledText
        // 3. Fire OnChunkResult with (partialText, isFinal)
        // 4. If isFinal → fire OnTranscriptionComplete with assembledText
        yield break;
    }
    */
}
