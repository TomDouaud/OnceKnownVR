using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Networking;
using System.Collections;
using System;
using System.Collections.Generic;
using System.Text;

public class VRPushToTalk : MonoBehaviour
{
    [Header("Configuration Touche")]
    public InputActionProperty talkAction; 

    [Header("Paramètres API")]
    public string apiUrl = "https://lordnns.myftp.org/api/ai/stt/transcribe";
    public string mlUrl  = "https://lordnns.myftp.org/api/ai/ml/analyze";
    public string llmUrl = "https://lordnns.myftp.org/api/ai/llm/chat";
    public string apiSecret = "Pure-Gallery-Silence-01-Ethereal!";

    [Header("Contexte Musée")]
    // Set this from your game logic when the player looks at / approaches an exhibit
    // Leave empty for general museum questions (RAG-only)
    public string currentArtifactId = "";

    [Header("Microphone")]
    // List of detected microphones — populated on Start and when RefreshMicrophones() is called
    public List<string> availableMicrophones = new List<string>();
    // Change this at runtime (from UI dropdown, etc.) to switch mic. Index into availableMicrophones.
    public int selectedMicrophoneIndex = 0;

    private AudioClip recording;
    private bool isRecording = false;
    private string deviceName;
    private const int SAMPLING_RATE = 16000;

    // ── LLM COLLECTOR ───────────────────────────────────────────────────
    // Both start null each recording cycle.
    // Once both are filled, the LLM call fires automatically.
    private string pendingTranscription = null;
    private string pendingEmotion = null;
    private bool llmAlreadySent = false;

    void OnEnable()
    {
        if (talkAction.action != null)
            talkAction.action.Enable();
    }

    void OnDisable()
    {
        if (talkAction.action != null)
            talkAction.action.Disable();
    }
    
    void Start()
    {
        RefreshMicrophones();
    }

    /// <summary>
    /// Call this to re-scan available microphones (e.g. when a headset is plugged in).
    /// </summary>
    public void RefreshMicrophones()
    {
        availableMicrophones.Clear();
        availableMicrophones.AddRange(Microphone.devices);

        if (availableMicrophones.Count == 0)
        {
            Debug.LogError("Aucun micro détecté.");
            deviceName = null;
            return;
        }

        // Log all detected mics
        for (int i = 0; i < availableMicrophones.Count; i++)
        {
            Debug.Log($"<color=cyan>[MIC {i}]</color> {availableMicrophones[i]}");
        }

        // Clamp index and select
        SelectMicrophone(selectedMicrophoneIndex);
        
        if (VRDebugPanel.instance != null)
            VRDebugPanel.instance.PopulateMicDropdown();
    }

    /// <summary>
    /// Switch to a different microphone by index. Safe to call at runtime from UI.
    /// </summary>
    public void SelectMicrophone(int index)
    {
        if (availableMicrophones.Count == 0)
        {
            Debug.LogError("Aucun micro disponible.");
            return;
        }

        selectedMicrophoneIndex = Mathf.Clamp(index, 0, availableMicrophones.Count - 1);
        deviceName = availableMicrophones[selectedMicrophoneIndex];
        Debug.Log($"<color=green>[MIC] Sélectionné : </color>{deviceName} (index {selectedMicrophoneIndex})");

        // If we were recording with the old mic, stop it
        if (isRecording)
        {
            Microphone.End(null);
            isRecording = false;
        }
    }

    void Update()
    {
        if (talkAction.action == null) return;

        float triggerValue = talkAction.action.ReadValue<float>();

        if (triggerValue > 0.5f && !isRecording)
        {
            Debug.Log("Recording...");
            StartRecording();
        }
        else if (triggerValue < 0.5f && isRecording)
        {
            Debug.Log("Stop Recording...");
            StopRecording();
        }
    }

    void StartRecording()
    {
        isRecording = true;

        // Reset collector for this new cycle
        pendingTranscription = null;
        pendingEmotion = null;
        llmAlreadySent = false;

        Debug.Log("Microphone activé...");
        recording = Microphone.Start(deviceName, false, 10, SAMPLING_RATE);
    }

    void StopRecording()
    {
        isRecording = false;
        int lastPos = Microphone.GetPosition(deviceName);
        Microphone.End(deviceName);

        if (lastPos > 0)
        {
            byte[] wavData = ConvertToWav(recording, lastPos);

            // Fire both in parallel — they each report back to the collector
            StartCoroutine(SendToSTT(wavData));
            StartCoroutine(SendToML(wavData));
        }
    }

    // ── STT ─────────────────────────────────────────────────────────────
    IEnumerator SendToSTT(byte[] audioData)
    {
        WWWForm form = new WWWForm();
        form.AddBinaryData("file", audioData, "speech.wav", "audio/wav");

        Debug.Log("Envoi STT vers : " + apiUrl);
        using (UnityWebRequest request = UnityWebRequest.Post(apiUrl, form))
        {
            request.SetRequestHeader("x-vr-app-secret", apiSecret);
            yield return request.SendWebRequest();

            if (request.result == UnityWebRequest.Result.Success)
            {
                string transcription = request.downloadHandler.text;
                Debug.Log("<color=green>[STT] Transcription : </color>" + transcription);

                // Report to collector
                TryReadyLLM(transcription: transcription);
            }
            else
            {
                Debug.LogError($"Erreur STT : {request.error}");
            }
        }
    }

    // ── ML (Emotion) ────────────────────────────────────────────────────
    IEnumerator SendToML(byte[] audioData)
    {
        string mlUrl = "https://lordnns.myftp.org/api/ai/ml/analyze";

        // Utilisation de WWWForm (la méthode robuste pour votre Gateway)
        WWWForm form = new WWWForm();
        form.AddBinaryData("file", audioData, "capture.wav", "audio/wav");

        Debug.Log("Envoi ML vers : " + mlUrl);
        using (UnityWebRequest request = UnityWebRequest.Post(mlUrl, form))
        {
            request.SetRequestHeader("x-vr-app-secret", apiSecret);
            yield return request.SendWebRequest();

            if (request.result != UnityWebRequest.Result.Success)
            {
                string responseJson = request.downloadHandler.text;
                Debug.Log("<color=cyan>[ML] Réponse : </color>" + responseJson);

                // Parse the emotion from the ML response
                MlResponse mlResult = JsonUtility.FromJson<MlResponse>(responseJson);
                string emotion = (mlResult != null) ? mlResult.final_decision : "unknown";

                // Report to collector
                TryReadyLLM(emotion: emotion);
            }
            else
            {
                string jsonResponse = request.downloadHandler.text;
                Debug.Log("<color=grey>JSON brut reçu du serveur : </color>" + jsonResponse);
                
                MlResponse result = JsonUtility.FromJson<MlResponse>(jsonResponse);
                
                if (result != null && result.status == "success")
                {
                    string emotionComplete = GetFullEmotionName(result.final_decision);
                    string couleur = GetColorForEmotion(emotionComplete);
                    
                    TryReadyLLM(emotion: emotionComplete);
                
                    Debug.Log($"<color={couleur}>[ML AGENT] Émotion complète : {emotionComplete.ToUpper()}</color>");
                }
                else
                {
                    Debug.LogWarning("Réponse reçue, mais impossible de lire 'final_decision'.");
                }
            }
        }
    }

    // ── COLLECTOR — fires LLM once both pieces arrive ───────────────────
    void TryReadyLLM(string transcription = null, string emotion = null)
    {
        // Store whichever value just came in
        if (transcription != null) pendingTranscription = transcription;
        if (emotion != null)       pendingEmotion = emotion;

        // Both ready? Fire once.
        if (pendingTranscription != null && pendingEmotion != null && !llmAlreadySent)
        {
            llmAlreadySent = true;
            Debug.Log($"<color=yellow>[COLLECTOR] Both ready — STT: \"{pendingTranscription}\" | Emotion: {pendingEmotion}</color>");
            StartCoroutine(SendToLLM(pendingTranscription, pendingEmotion));
        }
    }

    private string GetFullEmotionName(string shortEmotion)
    {
        if (string.IsNullOrEmpty(shortEmotion)) return "Inconnu";
    
        switch(shortEmotion.ToLower())
        {
            case "neu": return "Neutral";
            case "hap": return "Happy";
            case "ang": return "Angry";
            case "sad": return "Sad";
            case "exc": return "Excited";
            case "fru": return "Frustrated";
            default: return shortEmotion; 
        }
    }
    
    // ── LLM STREAMING CALL ──────────────────────────────────────────────
    IEnumerator SendToLLM(string transcription, string emotion)
    {
        Debug.Log("<color=green>[LLM] Envoi au guide...</color>");

        string jsonBody = JsonUtility.ToJson(new LlmRequest
        {
            prompt = transcription,
            emotion = emotion,
            artifactId = string.IsNullOrEmpty(currentArtifactId) ? null : currentArtifactId,
            stream = true
        });

        Debug.Log("<color=yellow>[LLM] Payload : </color>" + jsonBody);

        byte[] bodyRaw = Encoding.UTF8.GetBytes(jsonBody);

        using (UnityWebRequest request = new UnityWebRequest(llmUrl, "POST"))
        {
            request.uploadHandler = new UploadHandlerRaw(bodyRaw);
            request.downloadHandler = new StreamingDownloadHandler();
            request.SetRequestHeader("Content-Type", "application/json");
            request.SetRequestHeader("x-vr-app-secret", apiSecret);

            var op = request.SendWebRequest();
            StreamingDownloadHandler streamHandler = (StreamingDownloadHandler)request.downloadHandler;

            // Poll for new chunks while the request is in-flight
            while (!op.isDone)
            {
                string chunk = streamHandler.ConsumeNewData();
                if (!string.IsNullOrEmpty(chunk))
                {
                    Debug.Log("<color=cyan>[LLM stream] </color>" + ParseSSEChunk(chunk));
                }
                yield return null;
            }

            // Grab any remaining data
            string remaining = streamHandler.ConsumeNewData();
            if (!string.IsNullOrEmpty(remaining))
            {
                Debug.Log("<color=cyan>[LLM stream] </color>" + ParseSSEChunk(remaining));
            }

            if (request.result == UnityWebRequest.Result.Success)
            {
                string fullResponse = streamHandler.GetFullText();
                Debug.Log("<color=green>[LLM COMPLETE] </color>" + fullResponse);

                // TODO: pass fullResponse + emotion to TTS or UI
            }
            else
            {
                Debug.LogError($"Erreur LLM (Code {request.responseCode}) : {request.error}");
            }
        }
    }

    // ── HELPERS ──────────────────────────────────────────────────────────
    
    /// Strips "data: " prefix from an SSE chunk for cleaner logging.
    private string ParseSSEChunk(string raw)
    {
        StringBuilder clean = new StringBuilder();
        string[] lines = raw.Split('\n');
        foreach (string line in lines)
        {
            string trimmed = line.Trim();
            if (trimmed.StartsWith("data: "))
            {
                string token = trimmed.Substring(6);
                if (token == "[DONE]") continue;
                clean.Append(token);
            }
        }
        return clean.ToString();
    }

    private string GetColorForEmotion(string emotion)
    {
        switch (emotion)
        {
            case "happy":     return "yellow";
            case "angry":     return "red";
            case "sad":       return "blue";
            case "surprised": return "orange";
            default:          return "green";
        }
    }

    // ── CUSTOM STREAMING DOWNLOAD HANDLER ───────────────────────────────
    private class StreamingDownloadHandler : DownloadHandlerScript
    {
        private StringBuilder fullText = new StringBuilder();
        private int lastReadIndex = 0;

        protected override bool ReceiveData(byte[] data, int dataLength)
        {
            string text = Encoding.UTF8.GetString(data, 0, dataLength);
            fullText.Append(text);
            return true;
        }

        public string ConsumeNewData()
        {
            if (fullText.Length <= lastReadIndex) return null;
            string newData = fullText.ToString(lastReadIndex, fullText.Length - lastReadIndex);
            lastReadIndex = fullText.Length;
            return newData;
        }

        public string GetFullText()
        {
            // Parse SSE format: strip "data: " prefixes, skip [DONE], join tokens
            StringBuilder clean = new StringBuilder();
            string[] lines = fullText.ToString().Split('\n');
            foreach (string line in lines)
            {
                string trimmed = line.Trim();
                if (trimmed.StartsWith("data: "))
                {
                    string token = trimmed.Substring(6); // remove "data: "
                    if (token == "[DONE]") continue;
                    if (token.StartsWith("[STATUS]")) continue;
                    clean.Append(token);
                }
                else if (trimmed == "data:")
                {
                    // empty data line = paragraph break
                    clean.Append("\n\n");
                }
            }
            return clean.ToString().Trim();
        }

        protected override void CompleteContent() { }
        protected override float GetProgress() => 0;
    }

    // ── WAV CONVERSION ──────────────────────────────────────────────────
    byte[] ConvertToWav(AudioClip clip, int length)
    {
        float[] samples = new float[length * clip.channels];
        clip.GetData(samples, 0);

        int hz = SAMPLING_RATE;
        int channels = clip.channels;
        int samplesCount = samples.Length;

        using (System.IO.MemoryStream memoryStream = new System.IO.MemoryStream())
        {
            using (System.IO.BinaryWriter writer = new System.IO.BinaryWriter(memoryStream))
            {
                writer.Write(System.Text.Encoding.ASCII.GetBytes("RIFF"));
                writer.Write(36 + samplesCount * 2);
                writer.Write(System.Text.Encoding.ASCII.GetBytes("WAVE"));

                writer.Write(System.Text.Encoding.ASCII.GetBytes("fmt "));
                writer.Write(16);
                writer.Write((short)1);
                writer.Write((short)channels);
                writer.Write(hz);
                writer.Write(hz * channels * 2);
                writer.Write((short)(channels * 2));
                writer.Write((short)16);

                writer.Write(System.Text.Encoding.ASCII.GetBytes("data"));
                writer.Write(samplesCount * 2);

                for (int i = 0; i < samplesCount; i++)
                {
                    short intSample = (short)(samples[i] * 32767f);
                    writer.Write(intSample);
                }
            }
            return memoryStream.ToArray();
        }
    }
}

// ── SERIALIZABLE CLASSES ────────────────────────────────────────────────

[System.Serializable]
public class LlmRequest
{
    public string prompt;
    public string emotion;
    public string artifactId;
    public bool stream;
}

[System.Serializable]
public class MlResponse
{
    public string status;
    public int segments_analyzed;
    public List<MlSegment> detail;
    public string final_decision;
}

[System.Serializable]
public class MlSegment
{
    public float start_sec;
    public string emotion;
    public float confidence;
}