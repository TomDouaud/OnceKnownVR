using UnityEngine;
using System.Collections.Generic;
using System;

public class AudioRecorder : MonoBehaviour
{
    public static AudioRecorder Instance { get; private set; }

    [Header("Microphone")]
    public List<string> availableMicrophones = new List<string>();
    public int selectedMicrophoneIndex = 0;

    public const int SAMPLING_RATE = 16000;
    private const int MAX_RECORD_SECONDS = 300; // Boucle de 5 minutes

    private AudioClip recording;
    private string deviceName;
    
    // Indique si le joueur est *en train d'appuyer sur le bouton*
    public bool IsRecording { get; private set; } 
    private int captureStartPos = 0;

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    void Start()
    {
        RefreshMicrophones();
    }

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

        SelectMicrophone(selectedMicrophoneIndex);

        if (VRDebugPanel.instance != null)
            VRDebugPanel.instance.PopulateMicDropdown();
    }

    public void SelectMicrophone(int index)
    {
        if (availableMicrophones.Count == 0) return;

        selectedMicrophoneIndex = Mathf.Clamp(index, 0, availableMicrophones.Count - 1);
        deviceName = availableMicrophones[selectedMicrophoneIndex];
        
        Debug.Log($"<color=green>[MIC] Sélectionné et allumé en continu : </color>{deviceName}");

        // On arrête l'ancien micro s'il tournait
        Microphone.End(null);
        
        // On DÉMARRE LE MICRO EN CONTINU ICI (true = boucle)
        recording = Microphone.Start(deviceName, true, MAX_RECORD_SECONDS, SAMPLING_RATE);
    }

    // ════════════════════════════════════════════════════════════════════════
    //  Recording (Système de Marque-Page)
    // ════════════════════════════════════════════════════════════════════════

    public void StartRecording()
    {
        if (IsRecording) return;
        IsRecording = true;
        
        // On place un marque page là où le joueur a commencé à parler
        captureStartPos = Microphone.GetPosition(deviceName);
    }
    
    public byte[] StopRecording()
    {
        if (!IsRecording) return null;
        IsRecording = false;

        int currentPos = Microphone.GetPosition(deviceName);
        
        // Calcul de la taille de l'audio
        int length = currentPos - captureStartPos;
        
        // Si la boucle de 5 minutes a recommencé à zéro pendant qu'il parlait
        if (length < 0) length += recording.samples;

        if (length <= 0) return null;
        return GetWavChunk(captureStartPos, length);
    }

    // Utilitaires pour l'Orchestrateur
    public int GetMicPosition() => Microphone.GetPosition(deviceName);
    public int GetClipSamples() => recording != null ? recording.samples : 0;

    // ════════════════════════════════════════════════════════════════════════
    //  Extraction Sécurisée (Gère la boucle infinie)
    // ════════════════════════════════════════════════════════════════════════

    public byte[] GetWavChunk(int startSample, int lengthSamples)
    {
        if (recording == null || lengthSamples <= 0) return null;

        float[] samples = new float[lengthSamples * recording.channels];
        int clipSamples = recording.samples;

        // Sécurité pour rester dans les limites
        startSample = startSample % clipSamples;
        if (startSample < 0) startSample += clipSamples;

        // Si le morceau ne dépasse pas la fin de la boucle de 5 minutes
        if (startSample + lengthSamples <= clipSamples)
        {
            recording.GetData(samples, startSample);
        }
        else // Cas complexe : on lit la fin de la boucle, puis on reprend au début
        {
            int firstPart = clipSamples - startSample;
            int secondPart = lengthSamples - firstPart;

            float[] part1 = new float[firstPart * recording.channels];
            recording.GetData(part1, startSample);
            Array.Copy(part1, 0, samples, 0, part1.Length);

            float[] part2 = new float[secondPart * recording.channels];
            recording.GetData(part2, 0);
            Array.Copy(part2, 0, samples, part1.Length, part2.Length);
        }

        return EncodeToWav(samples, recording.channels);
    }

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