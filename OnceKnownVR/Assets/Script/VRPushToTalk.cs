using UnityEngine;
using UnityEngine.InputSystem;

// ════════════════════════════════════════════════════════════════════════════
//  VRPushToTalk  (Orchestrator)
// ════════════════════════════════════════════════════════════════════════════

public class VRPushToTalk : MonoBehaviour
{
    [Header("Script Guide")]
    public GuideController robotController;

    [Header("Artifact Scanner")]
    public VRArtifactScanner artifactScanner;

    [Header("VR Input")]
    public InputActionProperty talkAction;

    // ── Collector state ────────────────────────────────────────────────────
    private string pendingTranscription = null;
    private string pendingEmotion       = null;
    private bool   llmAlreadySent       = false;
    private byte[] currentWavData       = null;

    // ── Chunking State ─────────────────────────────────────────────────────
    private System.Collections.IEnumerator chunkRoutineRef;
    private Coroutine chunkRoutine;
    private int   lastSamplePosition = 0;
    private const float CHUNK_INTERVAL_SEC = 7f;
    private const float OVERLAP_SEC        = 1f;

    // ════════════════════════════════════════════════════════════════════════
    //  Lifecycle
    // ════════════════════════════════════════════════════════════════════════

    void OnEnable()
    {
        if (talkAction.action != null)
        {
            talkAction.action.Enable();
            if (robotController != null)
                robotController.ChangeState(2);
        }

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
    //  Pipeline cancellation
    // ════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Hard-stops every stage of the AI pipeline.
    /// Safe to call even when nothing is running.
    /// </summary>
    public void CancelFullPipeline()
    {
        if (STTService.Instance  != null) STTService.Instance.Cancel();
        if (MLService.Instance   != null) MLService.Instance.Cancel();
        if (LLMService.Instance  != null) LLMService.Instance.Cancel();
        if (TTSService.Instance  != null) TTSService.Instance.Cancel();
        Debug.Log("<color=red>[Orchestrator] Pipeline cancelled.</color>");
    }

    // ════════════════════════════════════════════════════════════════════════
    //  Recording cycle & Chunking
    // ════════════════════════════════════════════════════════════════════════

    private void BeginRecordingCycle()
    {
        CancelFullPipeline();

        // Snapshot the artifact the visitor is pointing at right now
        if (LLMService.Instance != null)
            LLMService.Instance.currentArtifactId = artifactScanner != null ? artifactScanner.CurrentArtifactId : "";

        pendingTranscription = null;
        pendingEmotion       = null;
        currentWavData       = null;
        llmAlreadySent       = false;
        lastSamplePosition   = 0;

        AudioRecorder.Instance.StartRecording();
        STTService.Instance.BeginSession();

        if (chunkRoutine != null) StopCoroutine(chunkRoutine);
        chunkRoutine = StartCoroutine(ProcessChunksRoutine());
    }

    private void EndRecordingCycle()
    {
        if (chunkRoutine != null) StopCoroutine(chunkRoutine);

        int currentPos      = Microphone.GetPosition(null);
        int overlapSamples  = (int)(OVERLAP_SEC * AudioRecorder.SAMPLING_RATE);
        int startPos        = Mathf.Max(0, lastSamplePosition - overlapSamples);
        int length          = currentPos - startPos;

        if (length > 0)
        {
            byte[] finalWavData = AudioRecorder.Instance.GetWavChunk(startPos, length);
            STTService.Instance.FinalizeSession(finalWavData);
        }
        else
        {
            STTService.Instance.FinalizeSession(null);
        }

        currentWavData = AudioRecorder.Instance.StopRecording();
    }

    private System.Collections.IEnumerator ProcessChunksRoutine()
    {
        int overlapSamples = (int)(OVERLAP_SEC * AudioRecorder.SAMPLING_RATE);

        while (AudioRecorder.Instance != null && AudioRecorder.Instance.IsRecording)
        {
            yield return new WaitForSeconds(CHUNK_INTERVAL_SEC);

            int currentPos = Microphone.GetPosition(null);
            int startPos   = Mathf.Max(0, lastSamplePosition - overlapSamples);
            int length     = currentPos - startPos;

            if (length > 0)
            {
                byte[] chunkWav = AudioRecorder.Instance.GetWavChunk(startPos, length);
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
            MLService.Instance.AnalyzeMultimodal(currentWavData, e.Text);
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

            //string filler = FillerBank.Pick();
            Debug.Log($"<color=yellow>[COLLECTOR] Both ready — STT: \"{pendingTranscription}\" " +
                      $"| Emotion: {pendingEmotion}");

            if (TTSService.Instance != null)
            {
                TTSService.Instance.BeginSession();
                //TTSService.Instance.FeedToken(filler);
            }

            LLMService.Instance.Send(pendingTranscription, pendingEmotion);
        }
    }
}