using UnityEngine;
using UnityEngine.InputSystem;

// ════════════════════════════════════════════════════════════════════════════
//  VRPushToTalk  (Orchestrator)
//
//  This is now a thin controller.  It owns ONLY:
//      • The VR input action (trigger press / release)
//      • The "collector" logic that waits for both STT + ML before
//        firing the LLM.
//
//  All heavy lifting lives in the singleton services:
//      AudioRecorder   – microphone & WAV encoding
//      STTService      – speech-to-text
//      MLService       – emotion analysis
//      LLMService      – streaming LLM chat
//      TTSService      – text-to-speech (placeholder)
//
//  ── Data-flow diagram ─────────────────────────────────────────────────
//
//   [Trigger Press]
//       │
//       ├─► AudioRecorder.StartRecording()
//       └─► STTService.BeginSession()      // reset accumulators
//               │
//               │  (while held — future chunked STT path)
//               │  STTService.SendChunk(partialWav)
//               │      └──► OnChunkResult  → build partial text for UI
//               │
//   [Trigger Release]
//       │
//       ├─► wavData = AudioRecorder.StopRecording()
//       │
//       ├─► MLService.Analyze(wavData)            // needs full audio
//       │       └──► OnEmotionDetected ──┐
//       │                                │
//       └─► STTService.FinalizeSession(wavData)   // last chunk + finish
//               └──► OnTranscriptionComplete ──┐
//                                              │
//                    ┌─────────────────────────┘
//                    │  COLLECTOR: both arrived?
//                    ▼
//              LLMService.Send(text, emotion)
//                    │
//                    ├──► OnTokenReceived   → TTSService.FeedToken(token)
//                    │                        (+ update UI live)
//                    │
//                    └──► OnResponseComplete → TTSService.Finalize()
//                                              (+ final UI update)
// ════════════════════════════════════════════════════════════════════════════

public class VRPushToTalk : MonoBehaviour
{
    [Header("VR Input")]
    public InputActionProperty talkAction;

    // ── Collector state ────────────────────────────────────────────────────
    private string pendingTranscription = null;
    private string pendingEmotion       = null;
    private bool   llmAlreadySent       = false;

    // ════════════════════════════════════════════════════════════════════════
    //  Lifecycle
    // ════════════════════════════════════════════════════════════════════════

    void OnEnable()
    {
        if (talkAction.action != null)
            talkAction.action.Enable();

        // Subscribe to service events
        if (STTService.Instance != null)
            STTService.Instance.OnTranscriptionComplete += OnSTTComplete;
        if (MLService.Instance != null)
            MLService.Instance.OnEmotionDetected += OnMLComplete;
        if (LLMService.Instance != null)
        {
            LLMService.Instance.OnTokenReceived    += OnLLMToken;
            LLMService.Instance.OnResponseComplete += OnLLMComplete;
        }
    }

    void OnDisable()
    {
        if (talkAction.action != null)
            talkAction.action.Disable();

        if (STTService.Instance != null)
            STTService.Instance.OnTranscriptionComplete -= OnSTTComplete;
        if (MLService.Instance != null)
            MLService.Instance.OnEmotionDetected -= OnMLComplete;
        if (LLMService.Instance != null)
        {
            LLMService.Instance.OnTokenReceived    -= OnLLMToken;
            LLMService.Instance.OnResponseComplete -= OnLLMComplete;
        }
    }

    // ════════════════════════════════════════════════════════════════════════
    //  Update — input polling  (same trigger logic as before)
    // ════════════════════════════════════════════════════════════════════════

    void Update()
    {
        if (talkAction.action == null || !AudioRecorder.Instance) return;

        float triggerValue = talkAction.action.ReadValue<float>();

        if (triggerValue > 0.5f && !AudioRecorder.Instance.IsRecording)
        {
            Debug.Log("Recording...");
            BeginRecordingCycle();
        }
        else if (triggerValue < 0.5f && AudioRecorder.Instance.IsRecording)
        {
            Debug.Log("Stop Recording...");
            EndRecordingCycle();
        }
    }

    // ════════════════════════════════════════════════════════════════════════
    //  Recording cycle
    // ════════════════════════════════════════════════════════════════════════

    private void BeginRecordingCycle()
    {
        // Reset collector
        pendingTranscription = null;
        pendingEmotion       = null;
        llmAlreadySent       = false;

        AudioRecorder.Instance.StartRecording();
        STTService.Instance.BeginSession();
    }

    private void EndRecordingCycle()
    {
        byte[] wavData = AudioRecorder.Instance.StopRecording();
        if (wavData == null || wavData.Length == 0) return;

        // ML needs the full audio — send it now
        MLService.Instance.Analyze(wavData);

        // STT: finalize (send full audio in current single-shot mode;
        //       in future chunked mode this sends the last chunk + "end" flag)
        STTService.Instance.FinalizeSession(wavData);
    }

    // ════════════════════════════════════════════════════════════════════════
    //  Service event handlers
    // ════════════════════════════════════════════════════════════════════════

    private void OnSTTComplete(object sender, STTChunkEventArgs e)
    {
        Debug.Log("<color=green>[STT → Orchestrator] </color>" + e.Text);
        TryFireLLM(transcription: e.Text);
    }

    private void OnMLComplete(object sender, MLResultEventArgs e)
    {
        Debug.Log($"<color=yellow>[ML → Orchestrator] </color>{e.Emotion}");
        TryFireLLM(emotion: e.Emotion);
    }

    private void OnLLMToken(object sender, LLMTokenEventArgs e)
    {
        // Forward streaming tokens to TTS (will be a no-op until implemented)
        if (TTSService.Instance != null)
            TTSService.Instance.FeedToken(e.Token);

        // TODO: also push token to UI subtitle display
    }

    private void OnLLMComplete(object sender, LLMCompleteEventArgs e)
    {
        if (e.Success)
        {
            Debug.Log("<color=green>[LLM → Orchestrator] Full response ready.</color>");

            // Signal TTS that the text stream is done
            if (TTSService.Instance != null)
                TTSService.Instance.Complete();

            // TODO: hand e.FullResponse + emotion to any remaining consumers
        }
        else
        {
            Debug.LogError("[LLM → Orchestrator] Failed: " + e.Error);
        }
    }

    // ════════════════════════════════════════════════════════════════════════
    //  Collector — fires LLM once both STT & ML have reported in
    //  (same logic as the original TryReadyLLM)
    // ════════════════════════════════════════════════════════════════════════

    private void TryFireLLM(string transcription = null, string emotion = null)
    {
        if (transcription != null) pendingTranscription = transcription;
        if (emotion != null)       pendingEmotion       = emotion;

        if (pendingTranscription != null && pendingEmotion != null && !llmAlreadySent)
        {
            llmAlreadySent = true;

            Debug.Log($"<color=yellow>[COLLECTOR] Both ready — STT: \"{pendingTranscription}\" " +
                      $"| Emotion: {pendingEmotion}</color>");

            // Start TTS session so it's ready to receive tokens
            if (TTSService.Instance != null)
                TTSService.Instance.BeginSession();

            LLMService.Instance.Send(pendingTranscription, pendingEmotion);
        }
    }
}
