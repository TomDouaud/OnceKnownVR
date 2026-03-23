using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Networking;
using System.Collections;
using System;
using System.Collections.Generic;

public class VRPushToTalk : MonoBehaviour
{
    [Header("Configuration Touche")]
    public InputActionProperty talkAction; 

    [Header("Paramètres API")]
    // Pré-rempli avec les infos de ton infrastructure
    public string apiUrl = "https://lordnns.myftp.org/api/ai/stt"; 
    public string apiSecret = "Pure-Gallery-Silence-01-Ethereal!";

    private AudioClip recording;
    private bool isRecording = false;
    private string deviceName;
    private const int SAMPLING_RATE = 16000;

    void OnEnable()
    {
        // Active l'écoute de la touche quand le script est activé
        if (talkAction.action != null)
        {
            talkAction.action.Enable();
        }
    }

    void OnDisable()
    {
        // Désactive l'écoute pour éviter les bugs quand l'objet est détruit/masqué
        if (talkAction.action != null)
        {
            talkAction.action.Disable();
        }
    }
    
    void Start()
    {
        if (Microphone.devices.Length > 0)
        {
            Debug.Log("Micro détecté : " + Microphone.devices[0]);
            deviceName = Microphone.devices[0];
        }
        else
            Debug.LogError("Aucun micro détecté.");
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
        Debug.Log("Microphone activé...");
        recording = Microphone.Start(deviceName, false, 10, SAMPLING_RATE);
    }

    void StopRecording()
    {
        isRecording = false;
        int lastPos = Microphone.GetPosition(deviceName);
        Microphone.End(deviceName);

        // Si on a bien enregistré quelque chose, on l'envoie
        if (lastPos > 0)
        {
            byte[] wavData = ConvertToWav(recording, lastPos);
            StartCoroutine(SendToSTT(wavData));
            StartCoroutine(EnvoyerAudioCoroutine(wavData));
        }
    }

    IEnumerator SendToSTT(byte[] audioData)
    {
        WWWForm form = new WWWForm();
        form.AddBinaryData("file", audioData, "speech.wav", "audio/wav");

        Debug.Log("Envoi vers : " + apiUrl);
        using (UnityWebRequest request = UnityWebRequest.Post(apiUrl, form))
        {
            request.SetRequestHeader("x-vr-app-secret", apiSecret);

            yield return request.SendWebRequest();

            if (request.result == UnityWebRequest.Result.Success)
            {
                string transcription = request.downloadHandler.text;
                
                // Appel de la méthode pour transmettre au guide
                EnvoyerALLM(transcription, audioData);
                EnvoyerML(audioData);
            }
            else
            {
                Debug.LogError($"Erreur de transcription : {request.error}");
            }
        }
    }

    public IEnumerator EnvoyerAudioCoroutine(byte[] audioData)
    {
        string url = "http://100.113.97.21:3000/analyze";

        // 1. Création du formulaire Multipart pour l'audio uniquement
        List<IMultipartFormSection> formData = new List<IMultipartFormSection>();

        // IMPORTANT : Le nom "file" doit correspondre à upload.single('file') dans ton index.js
        formData.Add(new MultipartFormFileSection("file", audioData, "capture_vocale.wav", "audio/wav"));

        using (UnityWebRequest www = UnityWebRequest.Post(url, formData))
        {
            yield return www.SendWebRequest();

            if (www.result != UnityWebRequest.Result.Success)
            {
                Debug.LogError("<color=red>Erreur Liaison ML : </color>" + www.error);
            }
            else
            {
                // Réception de l'analyse (ex: {"detected_emotion": "happy", "instruction_llm": "..."})
                Debug.Log("<color=cyan>Analyse d'émotion reçue : </color>" + www.downloadHandler.text);
            }
        }
    }


    void EnvoyerML( byte[] audioData)
    {
        Debug.Log("<color=green>Envoi de la question au guide : </color>" + audioData.Length + " bytes");
        StartCoroutine(EnvoyerAudioCoroutine(audioData));
    }
    
    
    
    void EnvoyerALLM(string transcription, byte[] audioData)
    {
        WWWForm form = new WWWForm();
        form.AddBinaryData("file", audioData, "speech.wav", "audio/wav");
        Debug.Log("<color=green>Envoi de la question au guide : </color>" + transcription);
    }

    // --- NOUVELLE CONVERSION AVEC EN-TÊTE WAV OFFICIEL ---
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
                // 1. CRÉATION DE L'EN-TÊTE WAV (44 octets)
                writer.Write(System.Text.Encoding.ASCII.GetBytes("RIFF"));
                writer.Write(36 + samplesCount * 2); 
                writer.Write(System.Text.Encoding.ASCII.GetBytes("WAVE"));
                
                writer.Write(System.Text.Encoding.ASCII.GetBytes("fmt "));
                writer.Write(16); // Taille du chunk fmt
                writer.Write((short)1); // Format audio (1 = PCM)
                writer.Write((short)channels); // Nombre de canaux
                writer.Write(hz); // Fréquence d'échantillonnage
                writer.Write(hz * channels * 2); // Byte rate
                writer.Write((short)(channels * 2)); // Block align
                writer.Write((short)16); // Bits par échantillon

                writer.Write(System.Text.Encoding.ASCII.GetBytes("data"));
                writer.Write(samplesCount * 2); // Taille des données audio

                // 2. ÉCRITURE DES DONNÉES AUDIO (PCM 16-bit)
                for (int i = 0; i < samplesCount; i++)
                {
                    // Convertit les float d'Unity (-1.0 à 1.0) en short PCM (-32768 à 32767)
                    short intSample = (short)(samples[i] * 32767f);
                    writer.Write(intSample);
                }
            }
            return memoryStream.ToArray();
        }
    }
}