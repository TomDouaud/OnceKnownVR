using UnityEngine;
using UnityEngine.Networking;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;

public class TTSService : MonoBehaviour
{
    public static TTSService Instance { get; private set; }

    [Header("Endpoint")]
    public string ttsUrl    = "https://lordnns.myftp.org/api/ai/tts/speak";
    public string apiSecret = "Pure-Gallery-Silence-01-Ethereal!";

    [Header("Voice")]
    public string voice      = "upmc";
    public int    speakerId  = 1;

    [Header("Tuning")]
    [Range(0.5f, 2.0f)]
    public float lengthScale = 1.2f;

    [Header("Audio")]
    public AudioSource audioSource;

    public event EventHandler<TTSEventArgs> OnTTSEvent;
    public bool IsSpeaking { get; private set; }

    private StringBuilder    _buffer        = new StringBuilder();
    private Queue<AudioClip> _queue         = new Queue<AudioClip>();
    private bool             _llmDone       = false;
    private int              _pending       = 0;
    private Coroutine        _player        = null;

    private static readonly char[] Boundaries = { '.', '!', '?', ';' };

    // ── Lifecycle ─────────────────────────────────────────────────────────

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    void Start()
    {
        if (audioSource == null)
            audioSource = gameObject.AddComponent<AudioSource>();
    }

    // ── Public API ────────────────────────────────────────────────────────

    public void BeginSession()
    {
        IsSpeaking = true;
        _llmDone   = false;
        _pending   = 0;
        _buffer.Clear();
        _queue.Clear();

        if (_player != null) StopCoroutine(_player);
        _player = StartCoroutine(PlayQueue());

        OnTTSEvent?.Invoke(this, new TTSEventArgs(TTSEventArgs.Phase.Started));
        Debug.Log("[TTS] Session started.");
    }

    public void FeedToken(string token)
    {
        if (!IsSpeaking || string.IsNullOrEmpty(token)) return;
        _buffer.Append(token);
        FlushAtBoundary();
    }

    public void Complete()
    {
        if (!IsSpeaking) return;
        _llmDone = true;

        string rest = _buffer.ToString().Trim();
        _buffer.Clear();
        if (!string.IsNullOrEmpty(rest)) Send(rest);
    }

    public void Cancel()
    {
        IsSpeaking = false;
        _pending   = 0;
        audioSource.Stop();
        _queue.Clear();
        if (_player != null) StopCoroutine(_player);
        Debug.Log("[TTS] Cancelled.");
    }

    // ── Boundary detection ────────────────────────────────────────────────

    private void FlushAtBoundary()
    {
        string buf = _buffer.ToString();
        int idx    = buf.LastIndexOfAny(Boundaries);

        if (idx < 0 && buf.Length > 80 && buf.Contains(","))
            idx = buf.LastIndexOf(',');

        if (idx < 0) return;

        string sentence = buf.Substring(0, idx + 1).Trim();
        _buffer.Clear();
        _buffer.Append(buf.Substring(idx + 1));

        if (!string.IsNullOrEmpty(sentence)) Send(sentence);
    }

    // ── Send to server ────────────────────────────────────────────────────

    [Serializable] private class Req
    {
        public string text;
        public string voice;
        public int    sid;
        public float  lengthScale;
    }

    private void Send(string sentence)
    {
        _pending++;
        StartCoroutine(Fetch(sentence));
    }

    private IEnumerator Fetch(string sentence)
    {
        Debug.Log($"[TTS] → \"{sentence}\"");

        byte[] body = Encoding.UTF8.GetBytes(JsonUtility.ToJson(new Req
        {
            text        = sentence,
            voice       = voice,
            sid         = speakerId,
            lengthScale = lengthScale
        }));

        using var req = new UnityWebRequest(ttsUrl, "POST");
        req.uploadHandler   = new UploadHandlerRaw(body);
        req.downloadHandler = new DownloadHandlerBuffer();
        req.SetRequestHeader("Content-Type",    "application/json");
        req.SetRequestHeader("x-vr-app-secret", apiSecret);

        yield return req.SendWebRequest();

        if (req.result != UnityWebRequest.Result.Success)
        {
            Debug.LogError("[TTS] " + req.error);
            _pending--;
            yield break;
        }

        AudioClip clip = WavToClip(req.downloadHandler.data);
        if (clip != null)
        {
            _queue.Enqueue(clip);
            OnTTSEvent?.Invoke(this, new TTSEventArgs(TTSEventArgs.Phase.ChunkReady, new AudioClipHandle { UnityClip = clip }));
        }

        _pending--;
    }

    // ── Player ────────────────────────────────────────────────────────────

    private IEnumerator PlayQueue()
    {
        while (true)
        {
            if (_queue.Count > 0 && !audioSource.isPlaying)
            {
                var clip = _queue.Dequeue();
                audioSource.clip = clip;
                audioSource.Play();
                Debug.Log($"[TTS] ♪ {clip.length:F2}s");
                yield return new WaitForSeconds(clip.length);
            }
            else if (_llmDone && _pending == 0 && _queue.Count == 0 && !audioSource.isPlaying)
            {
                IsSpeaking = false;
                OnTTSEvent?.Invoke(this, new TTSEventArgs(TTSEventArgs.Phase.Complete));
                Debug.Log("[TTS] Done.");
                yield break;
            }
            else yield return null;
        }
    }

    // ── WAV decoder ───────────────────────────────────────────────────────

    private static AudioClip WavToClip(byte[] wav)
    {
        try
        {
            int channels   = wav[22] | (wav[23] << 8);
            int sampleRate = wav[24] | (wav[25] << 8) | (wav[26] << 16) | (wav[27] << 24);
            int bitDepth   = wav[34] | (wav[35] << 8);

            int offset = 12;
            while (offset + 8 < wav.Length)
            {
                string id   = Encoding.ASCII.GetString(wav, offset, 4);
                int    size = wav[offset+4] | (wav[offset+5]<<8) | (wav[offset+6]<<16) | (wav[offset+7]<<24);
                if (id == "data") { offset += 8; break; }
                offset += 8 + size;
            }

            int     count   = (wav.Length - offset) / (bitDepth / 8) / channels;
            float[] samples = new float[count * channels];

            for (int i = 0; i < samples.Length; i++)
            {
                int o = offset + i * (bitDepth / 8);
                samples[i] = bitDepth == 16
                    ? (short)(wav[o] | (wav[o+1] << 8)) / 32768f
                    : BitConverter.ToSingle(wav, o);
            }

            var clip = AudioClip.Create("tts", count, channels, sampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }
        catch (Exception e)
        {
            Debug.LogError("[TTS] WAV error: " + e.Message);
            return null;
        }
    }
}