using UnityEngine;
using System.Collections.Generic;

// ════════════════════════════════════════════════════════════════════════════
//  AudioRecorder  (Singleton)
//  Owns the microphone: start / stop recording, WAV conversion,
//  mic enumeration & selection.
// ════════════════════════════════════════════════════════════════════════════

public class AudioRecorder : MonoBehaviour
{
    // ── Singleton ──────────────────────────────────────────────────────────
    public static AudioRecorder Instance { get; private set; }

    // ── Inspector ──────────────────────────────────────────────────────────
    [Header("Microphone")]
    public List<string> availableMicrophones = new List<string>();
    public int selectedMicrophoneIndex = 0;

    // ── Constants ──────────────────────────────────────────────────────────
    public const int SAMPLING_RATE = 16000;
    private const int MAX_RECORD_SECONDS = 300; // Augmenté pour permettre de longues phrases

    // ── State ──────────────────────────────────────────────────────────────
    private AudioClip recording;
    private string    deviceName;
    public  bool      IsRecording { get; private set; }

    // ════════════════════════════════════════════════════════════════════════
    //  Lifecycle
    // ════════════════════════════════════════════════════════════════════════

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    void Start()
    {
        RefreshMicrophones();
    }

    // ════════════════════════════════════════════════════════════════════════
    //  Microphone management
    // ════════════════════════════════════════════════════════════════════════

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

        for (int i = 0; i < availableMicrophones.Count; i++)
            Debug.Log($"<color=cyan>[MIC {i}]</color> {availableMicrophones[i]}");

        SelectMicrophone(selectedMicrophoneIndex);

        if (VRDebugPanel.instance != null)
            VRDebugPanel.instance.PopulateMicDropdown();
    }

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

        if (IsRecording)
        {
            Microphone.End(null);
            IsRecording = false;
        }
    }

    // ════════════════════════════════════════════════════════════════════════
    //  Recording
    // ════════════════════════════════════════════════════════════════════════

    public void StartRecording()
    {
        if (IsRecording) return;
        IsRecording = true;
        Debug.Log("Microphone activé...");
        recording = Microphone.Start(deviceName, false, MAX_RECORD_SECONDS, SAMPLING_RATE);
    }
    
    public byte[] StopRecording()
    {
        if (!IsRecording) return null;
        IsRecording = false;

        int lastPos = Microphone.GetPosition(deviceName);
        Microphone.End(deviceName);

        if (lastPos <= 0) return null;
        return ConvertToWav(recording, lastPos);
    }
    
    public bool TryGetLiveBuffer(out AudioClip clip, out int samplesRecorded)
    {
        clip = recording;
        samplesRecorded = IsRecording ? Microphone.GetPosition(deviceName) : 0;
        return IsRecording && samplesRecorded > 0;
    }

    // ════════════════════════════════════════════════════════════════════════
    //  WAV conversion & Chunking
    // ════════════════════════════════════════════════════════════════════════

    /// Encode une sous-partie de l'AudioClip en WAV (Utile pour le chunking)
    public byte[] GetWavChunk(int startSample, int lengthSamples)
    {
        if (recording == null) return null;

        if (startSample < 0) startSample = 0;
        if (lengthSamples <= 0) return null;
        if (startSample + lengthSamples > recording.samples) 
            lengthSamples = recording.samples - startSample;

        float[] samples = new float[lengthSamples * recording.channels];
        recording.GetData(samples, startSample);

        return EncodeToWav(samples, recording.channels);
    }

    /// Encode tout l'audio depuis le début
    public static byte[] ConvertToWav(AudioClip clip, int sampleLength)
    {
        float[] samples = new float[sampleLength * clip.channels];
        clip.GetData(samples, 0);
        return EncodeToWav(samples, clip.channels);
    }

    /// Le moteur d'encodage WAV (Header 44 octets + PCM 16-bit)
    private static byte[] EncodeToWav(float[] samples, int channels)
    {
        int hz = SAMPLING_RATE;
        int count = samples.Length;

        using (var ms = new System.IO.MemoryStream())
        using (var w  = new System.IO.BinaryWriter(ms))
        {
            w.Write(System.Text.Encoding.ASCII.GetBytes("RIFF"));
            w.Write(36 + count * 2);
            w.Write(System.Text.Encoding.ASCII.GetBytes("WAVE"));

            w.Write(System.Text.Encoding.ASCII.GetBytes("fmt "));
            w.Write(16);
            w.Write((short)1);
            w.Write((short)channels);
            w.Write(hz);
            w.Write(hz * channels * 2);
            w.Write((short)(channels * 2));
            w.Write((short)16);

            w.Write(System.Text.Encoding.ASCII.GetBytes("data"));
            w.Write(count * 2);

            for (int i = 0; i < count; i++)
            {
                short s = (short)(samples[i] * 32767f);
                w.Write(s);
            }
            return ms.ToArray();
        }
    }
}