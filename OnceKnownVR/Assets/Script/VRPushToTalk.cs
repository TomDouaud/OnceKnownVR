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
using UnityEngine;
using UnityEngine.InputSystem;

public class VRPushToTalk : MonoBehaviour
{
    [Header("VR Input")]
    public InputActionProperty talkAction;

    // ── Collector state ────────────────────────────────────────────────────
    private string pendingTranscription = null;
    private string pendingEmotion       = null;
    private bool   llmAlreadySent       = false;
    private byte[] currentWavData       = null;

    // ── Chunking State ─────────────────────────────────────────────────────
    private Coroutine chunkRoutine;
    private int lastSamplePosition = 0;
    private const float CHUNK_INTERVAL_SEC = 7f; // On traite tous les 7s
    private const float OVERLAP_SEC = 1f;        // 1s de retour en arrière (total = 8s envoyées)

    // ════════════════════════════════════════════════════════════════════════
    //  Lifecycle
    // ════════════════════════════════════════════════════════════════════════

    void OnEnable()
    {
        if (talkAction.action != null)
            talkAction.action.Enable();

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
    //  Update
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
    //  Recording cycle & Chunking
    // ════════════════════════════════════════════════════════════════════════

    private void BeginRecordingCycle()
    {
        pendingTranscription = null;
        pendingEmotion       = null;
        currentWavData       = null;
        llmAlreadySent       = false;
        lastSamplePosition   = 0;

        AudioRecorder.Instance.StartRecording();
        STTService.Instance.BeginSession();

        // On lance l'horloge des chunks
        if (chunkRoutine != null) StopCoroutine(chunkRoutine);
        chunkRoutine = StartCoroutine(ProcessChunksRoutine());
    }

    private void EndRecordingCycle()
    {
        if (chunkRoutine != null) StopCoroutine(chunkRoutine);

        // On gère le tout dernier morceau d'audio (ce qu'il reste depuis le dernier chunk)
        int currentPos = Microphone.GetPosition(null);
        int overlapSamples = (int)(OVERLAP_SEC * AudioRecorder.SAMPLING_RATE);
        int startPos = Mathf.Max(0, lastSamplePosition - overlapSamples);
        int length = currentPos - startPos;

        if (length > 0)
        {
            byte[] finalWavData = AudioRecorder.Instance.GetWavChunk(startPos, length);
            STTService.Instance.FinalizeSession(finalWavData);
        }
        else
        {
            STTService.Instance.FinalizeSession(null);
        }

        // Pour l'analyse d'émotion (ML), on envoie l'audio complet à la fin
        currentWavData = AudioRecorder.Instance.StopRecording();
    }

    private System.Collections.IEnumerator ProcessChunksRoutine()
    {
        int overlapSamples = (int)(OVERLAP_SEC * AudioRecorder.SAMPLING_RATE);

        while (AudioRecorder.Instance != null && AudioRecorder.Instance.IsRecording)
        {
            // On attend le temps du palier (7s)
            yield return new WaitForSeconds(CHUNK_INTERVAL_SEC);

            int currentPos = Microphone.GetPosition(null);
            int startPos = Mathf.Max(0, lastSamplePosition - overlapSamples);
            int length = currentPos - startPos;

            if (length > 0)
            {
                byte[] chunkWav = AudioRecorder.Instance.GetWavChunk(startPos, length);
                // On envoie le chunk partiel au STT !
                STTService.Instance.SendChunk(chunkWav);
                
                lastSamplePosition = currentPos;
            }
        }
    }

    // ════════════════════════════════════════════════════════════════════════
    //  Service event handlers
    // ════════════════════════════════════════════════════════════════════════

    private void OnSTTComplete(object sender, STTChunkEventArgs e)
    {
        Debug.Log("<color=green>[STT → Orchestrator] </color>" + e.Text);
        TryFireLLM(transcription: e.Text);
        if (MLService.Instance != null && currentWavData != null && currentWavData.Length > 0)
        {
            MLService.Instance.AnalyzeMultimodal(currentWavData, e.Text);
        }
    }

    private void OnMLComplete(object sender, MLResultEventArgs e)
    {
        Debug.Log($"<color=yellow>[ML → Orchestrator] </color>{e.Emotion}");
        TryFireLLM(emotion: e.Emotion);
    }

    private void OnLLMToken(object sender, LLMTokenEventArgs e)
    {
        if (TTSService.Instance != null)
            TTSService.Instance.FeedToken(e.Token);
    }

    private void OnLLMComplete(object sender, LLMCompleteEventArgs e)
    {
        if (e.Success)
        {
            Debug.Log("<color=green>[LLM → Orchestrator] Full response ready.</color>");
            if (TTSService.Instance != null)
                TTSService.Instance.Complete();
        }
        else
        {
            Debug.LogError("[LLM → Orchestrator] Failed: " + e.Error);
        }
    }

    private void TryFireLLM(string transcription = null, string emotion = null)
    {
        if (transcription != null) pendingTranscription = transcription;
        if (emotion != null)       pendingEmotion       = emotion;

        if (pendingTranscription != null && pendingEmotion != null && !llmAlreadySent)
        {
            llmAlreadySent = true;
            
            string filler = FillerBank.Pick();

            Debug.Log($"<color=yellow>[COLLECTOR] Both ready — STT: \"{pendingTranscription}\" " +
                      $"| Emotion: {pendingEmotion} | Filler: \"{filler}\"</color>");

            if (TTSService.Instance != null)
            {
                TTSService.Instance.BeginSession();
                TTSService.Instance.FeedToken(filler);
            }

            LLMService.Instance.Send(pendingTranscription, pendingEmotion, filler);
        }
    }
}