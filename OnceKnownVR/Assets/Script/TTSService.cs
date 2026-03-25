using UnityEngine;
using System;

// ════════════════════════════════════════════════════════════════════════════
//  TTSService  (Singleton)
//
//  Text-to-Speech service — PLACEHOLDER.
//
//  Final implementation will be one of:
//      A) Local on-device inference (ONNX model on Quest)
//      B) Server-side synthesis via REST/WebSocket
//      C) Hybrid — try local first, fall back to server
//
//  The public API is designed so callers don't care which backend is used.
//
//  Typical flow:
//      1. LLM streams tokens  → TTSService.FeedToken(token)
//         (buffer tokens until a sentence boundary, then synthesise)
//      2. LLM completes       → TTSService.Finalize()
//         (flush remaining buffer)
//      3. TTSService fires OnTTSEvent(ChunkReady) with an AudioClip
//         each time a chunk of speech is ready for playback.
//      4. TTSService fires OnTTSEvent(Complete) when all speech is done.
// ════════════════════════════════════════════════════════════════════════════

public class TTSService : MonoBehaviour
{
    // ── Singleton ──────────────────────────────────────────────────────────
    public static TTSService Instance { get; private set; }

    // ── Configuration ─────────────────────────────────────────────────────
    [Header("TTS Backend")]
    [Tooltip("If true, attempt local ONNX inference. Otherwise use server.")]
    public bool useLocalModel = false;

    [Header("Server Endpoint (if not local)")]
    public string ttsUrl    = "";
    public string apiSecret = "Pure-Gallery-Silence-01-Ethereal!";

    // ── Events ─────────────────────────────────────────────────────────────
    /// Fired on TTS state changes: Started, ChunkReady (with AudioClip), Complete, Error.
    public event EventHandler<TTSEventArgs> OnTTSEvent;

    // ── State ──────────────────────────────────────────────────────────────
    public bool IsSpeaking { get; private set; }

    // ════════════════════════════════════════════════════════════════════════
    //  Lifecycle
    // ════════════════════════════════════════════════════════════════════════

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    // ════════════════════════════════════════════════════════════════════════
    //  Public API  (stubs — implement when backend is decided)
    // ════════════════════════════════════════════════════════════════════════
    
    /// Begin a new TTS session.  Call before feeding tokens.
    public void BeginSession()
    {
        IsSpeaking = true;
        Debug.Log("[TTS] Session started (stub).");
        OnTTSEvent?.Invoke(this, new TTSEventArgs(TTSEventArgs.Phase.Started));
    }
    
    /// Feed a streaming token from the LLM.
    /// Internally buffers until a sentence boundary, then synthesises a chunk.
    public void FeedToken(string token)
    {
        if (!IsSpeaking) return;

        // ── TODO: buffer tokens, detect sentence boundaries, synthesise ──
        // Debug.Log("[TTS] Token buffered: " + token);
    }
    
    /// Signal that the LLM response is complete.  Flushes any remaining
    /// buffered text and fires Complete when the last audio chunk is ready.
    public void Complete()
    {
        if (!IsSpeaking) return;
        IsSpeaking = false;

        // ── TODO: flush buffer, synthesise last chunk ──
        Debug.Log("[TTS] Session finalized (stub).");
        OnTTSEvent?.Invoke(this, new TTSEventArgs(TTSEventArgs.Phase.Complete));
    }
    
    /// Immediately stop any ongoing synthesis and playback.
    public void Cancel()
    {
        IsSpeaking = false;
        Debug.Log("[TTS] Cancelled.");
    }
    
    /// One-shot: synthesise a complete string (e.g. the full LLM response).
    /// Useful when streaming token-by-token isn't needed.
    public void SpeakFull(string text)
    {
        Debug.Log("[TTS] SpeakFull (stub): " + text);

        // ── TODO: synthesise full text, fire ChunkReady / Complete ──
    }
}
