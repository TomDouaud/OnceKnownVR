using System;
using System.Collections.Generic;

// ════════════════════════════════════════════════════════════════════════════
//  Shared data models & event args for the AI service pipeline
// ════════════════════════════════════════════════════════════════════════════

// ── REQUEST / RESPONSE DTOs ────────────────────────────────────────────────

[Serializable]
public class LlmRequest
{
    public string prompt;
    public string emotion;
    public string artifactId;
    public bool   stream;
}

[Serializable]
public class MlResponse
{
    public string        status;
    public int           segments_analyzed;
    public List<MlSegment> detail;
    public string        final_decision;
}

[Serializable]
public class MlSegment
{
    public float  start_sec;
    public string emotion;
    public float  confidence;
}

// ── EVENT PAYLOADS ─────────────────────────────────────────────────────────

/// <summary>Fired every time STT produces a partial or final transcription chunk.</summary>
public class STTChunkEventArgs : EventArgs
{
    public string Text    { get; }
    public bool   IsFinal { get; }
    public STTChunkEventArgs(string text, bool isFinal)
    {
        Text    = text;
        IsFinal = isFinal;
    }
}

/// <summary>Fired when ML emotion analysis completes.</summary>
public class MLResultEventArgs : EventArgs
{
    public string Emotion    { get; }
    public string RawJson    { get; }
    public bool   Success    { get; }
    public string Error      { get; }
    public MLResultEventArgs(string emotion, string rawJson, bool success, string error = null)
    {
        Emotion = emotion;
        RawJson = rawJson;
        Success = success;
        Error   = error;
    }
}

/// <summary>Fired for each streamed LLM token.</summary>
public class LLMTokenEventArgs : EventArgs
{
    public string Token { get; }
    public LLMTokenEventArgs(string token) { Token = token; }
}

/// <summary>Fired when the LLM response is fully received.</summary>
public class LLMCompleteEventArgs : EventArgs
{
    public string FullResponse { get; }
    public bool   Success      { get; }
    public string Error        { get; }
    public LLMCompleteEventArgs(string fullResponse, bool success, string error = null)
    {
        FullResponse = fullResponse;
        Success      = success;
        Error        = error;
    }
}

/// <summary>Fired by TTS for progress / completion.</summary>
public class TTSEventArgs : EventArgs
{
    public enum Phase { Started, ChunkReady, Complete, Error }
    public Phase      CurrentPhase { get; }
    public AudioClipHandle Clip    { get; }   // nullable — only on ChunkReady
    public string     Error        { get; }
    public TTSEventArgs(Phase phase, AudioClipHandle clip = null, string error = null)
    {
        CurrentPhase = phase;
        Clip         = clip;
        Error        = error;
    }
}

/// Thin wrapper so the TTS event arg doesn't force a hard dependency on UnityEngine.AudioClip
/// in this model file.  Services set .UnityClip to the real AudioClip.
public class AudioClipHandle
{
    public object UnityClip { get; set; }   // cast to AudioClip where needed
}
