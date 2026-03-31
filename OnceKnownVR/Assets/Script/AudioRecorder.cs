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
    private const int MAX_RECORD_SECONDS = 10;

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

    ///Re-scan available microphones.
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

    ///Switch to a different microphone by index.
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

    ///Begin capturing audio from the selected microphone.
    public void StartRecording()
    {
        if (IsRecording) return;
        IsRecording = true;
        Debug.Log("Microphone activé...");
        recording = Microphone.Start(deviceName, false, MAX_RECORD_SECONDS, SAMPLING_RATE);
    }
    
    /// Stop capturing and return the full WAV byte array.
    /// Returns null if nothing meaningful was recorded.
    public byte[] StopRecording()
    {
        if (!IsRecording) return null;
        IsRecording = false;

        int lastPos = Microphone.GetPosition(deviceName);
        Microphone.End(deviceName);

        if (lastPos <= 0) return null;
        return ConvertToWav(recording, lastPos);
    }
    
    /// While recording is active, returns the current AudioClip reference
    /// and the number of samples recorded so far.
    /// Useful for STT chunked streaming — callers can read partial buffers.
    public bool TryGetLiveBuffer(out AudioClip clip, out int samplesRecorded)
    {
        clip = recording;
        samplesRecorded = IsRecording ? Microphone.GetPosition(deviceName) : 0;
        return IsRecording && samplesRecorded > 0;
    }

    // ════════════════════════════════════════════════════════════════════════
    //  WAV conversion 
    // ════════════════════════════════════════════════════════════════════════

    ///Encode raw PCM samples from an AudioClip into a standard 16-bit WAV byte array.
    public static byte[] ConvertToWav(AudioClip clip, int sampleLength)
    {
        float[] samples = new float[sampleLength * clip.channels];
        clip.GetData(samples, 0);

        int hz       = SAMPLING_RATE;
        int channels = clip.channels;
        int count    = samples.Length;

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
