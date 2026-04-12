using UnityEngine;
using UnityEngine.Networking;
using System;
using System.Collections;

// ════════════════════════════════════════════════════════════════════════════
//  MLService  (Singleton)
//
//  Emotion / ML analysis service.
// ════════════════════════════════════════════════════════════════════════════

public enum MLAnalysisType { Audio, Text, Multimodal }

public class MLService : MonoBehaviour
{
    // ── Singleton ──────────────────────────────────────────────────────────
    public static MLService Instance { get; private set; }

    // ── Configuration ─────────────────────────────────────────────────────
    [Header("ML Endpoint")]
    public string mlUrl     = "https://lordnns.myftp.org/api/ai/ml/analyze";
    public string apiSecret = "Pure-Gallery-Silence-01-Ethereal!";

    // ── Events ─────────────────────────────────────────────────────────────
    public event EventHandler<MLResultEventArgs> OnEmotionDetected;

    // ── Internal state ─────────────────────────────────────────────────────
    private bool _cancelled = false;

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

    /// Abort any in-flight ML requests and suppress completion events.
    public void Cancel()
    {
        _cancelled = true;
        StopAllCoroutines();
        Debug.Log("[ML] Cancelled.");
    }

    public void AnalyzeAudio(byte[] fullWavData)
    {
        _cancelled = false;
        StartCoroutine(PostData(MLAnalysisType.Audio, fullWavData, null));
    }

    public void AnalyzeText(string transcription)
    {
        _cancelled = false;
        StartCoroutine(PostData(MLAnalysisType.Text, null, transcription));
    }

    public void AnalyzeMultimodal(byte[] fullWavData, string transcription)
    {
        _cancelled = false;
        StartCoroutine(PostData(MLAnalysisType.Multimodal, fullWavData, transcription));
    }

    // ════════════════════════════════════════════════════════════════════════
    //  Network
    // ════════════════════════════════════════════════════════════════════════

    private IEnumerator PostData(MLAnalysisType type, byte[] audioData, string transcription)
    {
        WWWForm form = new WWWForm();

        if (audioData != null)               form.AddBinaryData("file", audioData, "capture.wav", "audio/wav");
        if (!string.IsNullOrEmpty(transcription)) form.AddField("transcription", transcription);

        string typeName = type.ToString().ToUpper();
        Debug.Log($"[ML] Envoi {typeName} vers : " + mlUrl);

        using (UnityWebRequest request = UnityWebRequest.Post(mlUrl, form))
        {
            request.SetRequestHeader("x-vr-app-secret", apiSecret);
            yield return request.SendWebRequest();

            // Discard result if cancelled while the request was in-flight
            if (_cancelled) yield break;

            if (request.result != UnityWebRequest.Result.Success)
            {
                Debug.Log($"<color=red>[ML] Erreur réseau ({typeName}) : </color>" + request.error);
                OnEmotionDetected?.Invoke(this, new MLResultEventArgs("unknown", request.downloadHandler.text, false, request.error));
            }
            else
            {
                string jsonResponse = request.downloadHandler.text;
                MlResponse result = JsonUtility.FromJson<MlResponse>(jsonResponse);

                if (result != null && result.status == "success")
                {
                    string emotionFull = GetFullEmotionName(result.final_decision);
                    string colour      = GetColorForEmotion(emotionFull);

                    Debug.Log($"<color={colour}>[ML AGENT - {typeName}] Émotion : {emotionFull.ToUpper()}</color>");

                    if (type == MLAnalysisType.Multimodal)
                        Debug.Log($"<color=grey>Détails -> Voix: {GetFullEmotionName(result.audio_detected)} | Texte: {GetFullEmotionName(result.text_detected)}</color>");

                    OnEmotionDetected?.Invoke(this, new MLResultEventArgs(emotionFull, jsonResponse, true));
                }
                else
                {
                    OnEmotionDetected?.Invoke(this, new MLResultEventArgs("unknown", jsonResponse, false, "Could not parse final_decision"));
                }
            }
        }
    }

    // ════════════════════════════════════════════════════════════════════════
    //  Helpers
    // ════════════════════════════════════════════════════════════════════════

    public static string GetFullEmotionName(string shortEmotion)
    {
        if (string.IsNullOrEmpty(shortEmotion)) return "Inconnu";
        switch (shortEmotion.ToLower())
        {
            case "neu": return "Neutral";
            case "hap": return "Happy";
            case "ang": return "Angry";
            case "sad": return "Sad";
            case "exc": return "Excited";
            case "fru": return "Frustrated";
            default:    return "Neutral";
        }
    }

    public static string GetColorForEmotion(string emotion)
    {
        switch (emotion.ToLower())
        {
            case "happy":     return "yellow";
            case "angry":     return "red";
            case "sad":       return "blue";
            case "surprised": return "orange";
            default:          return "green";
        }
    }
}