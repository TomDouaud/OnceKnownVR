using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using System.Collections.Generic;

public class VRDebugPanel : MonoBehaviour
{
    public static VRDebugPanel instance;

    [Header("Références")]
    public Transform xrOrigin;

    [Header("Toggle UI")]
    public InputActionProperty toggleAction;  // bind to a button like Menu/Y/B

    [Header("Position")]
    public Vector3 localPosition = new Vector3(0.5f, 1.2f, 1.5f);
    public Vector3 localRotation = new Vector3(15f, -10f, 0f);

    [Header("UI References (drag from prefab)")]
    public Canvas canvas;
    public Dropdown micDropdown;
    public Button refreshButton;
    public RectTransform logContent;
    public ScrollRect scrollRect;

    [Header("Log")]
    public int maxLogLines = 50;
    public float fontSize = 14f;
    public Font font;

    private List<GameObject> logEntries = new List<GameObject>();
    private bool panelVisible = true;

    private static readonly Dictionary<string, string> tagColors = new Dictionary<string, string>
    {
        { "[STT",        "#55FF55" },
        { "[ML",         "#55FFFF" },
        { "[LLM",        "#FFFF55" },
        { "[TTS",        "#FF88FF" },
        { "[COLLECTOR]", "#FFAA00" },
        { "[MIC",        "#55FFFF" },
        { "[STATUS]",    "#888888" },
        { "Orchestrator","#FFAA00" },
        { "Erreur",      "#FF5555" },
        { "Error",       "#FF5555" }
    };

    void Awake()
    {
        instance = this;
    }

    void OnEnable()
    {
        if (toggleAction.action != null)
        {
            toggleAction.action.Enable();
            toggleAction.action.performed += OnTogglePressed;
        }
    }

    void OnDisable()
    {
        if (toggleAction.action != null)
        {
            toggleAction.action.performed -= OnTogglePressed;
            toggleAction.action.Disable();
        }
    }

    void Start()
    {
        if (micDropdown != null)
            micDropdown.onValueChanged.AddListener(OnMicSelected);

        if (refreshButton != null)
            refreshButton.onClick.AddListener(OnRefreshClick);

        // Make sure content has a vertical layout for stacking log entries
        if (logContent != null && logContent.GetComponent<VerticalLayoutGroup>() == null)
        {
            VerticalLayoutGroup vlg = logContent.gameObject.AddComponent<VerticalLayoutGroup>();
            vlg.childForceExpandWidth = true;
            vlg.childForceExpandHeight = false;
            vlg.childControlWidth = true;
            vlg.childControlHeight = true;
            vlg.spacing = 2;
            vlg.padding = new RectOffset(8, 8, 4, 4);
        }

        // Make sure content has a ContentSizeFitter to grow with entries
        if (logContent != null && logContent.GetComponent<ContentSizeFitter>() == null)
        {
            ContentSizeFitter csf = logContent.gameObject.AddComponent<ContentSizeFitter>();
            csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        }

        // Clear any sample content that was in the prefab
        if (logContent != null)
        {
            foreach (Transform child in logContent)
                Destroy(child.gameObject);
        }

        PopulateMicDropdown();
        Application.logMessageReceived += OnLogMessage;
    }

    void OnDestroy()
    {
        Application.logMessageReceived -= OnLogMessage;
    }

    void LateUpdate()
    {
        if (canvas == null || xrOrigin == null || !panelVisible) return;

        canvas.transform.position = xrOrigin.position
            + xrOrigin.right   * localPosition.x
            + xrOrigin.up      * localPosition.y
            + xrOrigin.forward * localPosition.z;

        canvas.transform.rotation = xrOrigin.rotation * Quaternion.Euler(localRotation);
    }

    // ── MIC DROPDOWN ────────────────────────────────────────────────────
    public void PopulateMicDropdown()
    {
        if (micDropdown == null || AudioRecorder.Instance == null) return;

        micDropdown.ClearOptions();

        if (AudioRecorder.Instance.availableMicrophones.Count == 0)
        {
            micDropdown.AddOptions(new List<string> { "No microphone found" });
            return;
        }

        micDropdown.AddOptions(AudioRecorder.Instance.availableMicrophones);
        micDropdown.value = AudioRecorder.Instance.selectedMicrophoneIndex;
        micDropdown.RefreshShownValue();
    }

    void OnMicSelected(int index)
    {
        if (AudioRecorder.Instance != null)
            AudioRecorder.Instance.SelectMicrophone(index);
    }

    void OnRefreshClick()
    {
        if (AudioRecorder.Instance != null)
            AudioRecorder.Instance.RefreshMicrophones();
        AddLog("<color=#55FF55>Microphones refreshed</color>");
    }

    // ── TOGGLE ────────────────────────────────────────────────────────────
    void OnTogglePressed(InputAction.CallbackContext ctx)
    {
        TogglePanel();
    }

    public void TogglePanel()
    {
        if (canvas == null) return;

        panelVisible = !panelVisible;
        canvas.gameObject.SetActive(panelVisible);

        // Scroll to bottom when re-shown so latest logs are visible
        if (panelVisible && scrollRect != null)
        {
            Canvas.ForceUpdateCanvases();
            scrollRect.verticalNormalizedPosition = 0f;
        }
    }

    // ── LOG ──────────────────────────────────────────────────────────────
    void OnLogMessage(string message, string stackTrace, LogType type)
    {
        string prefix = "";
        switch (type)
        {
            case LogType.Error:
            case LogType.Exception:
                prefix = "<color=#FF5555>[ERR]</color> ";
                break;
            case LogType.Warning:
                prefix = "<color=#FFAA55>[WRN]</color> ";
                break;
        }

        string clean = StripUnityColorTags(message);
        string colored = ColorizeKnownTags(clean);
        AddLog(prefix + colored);
    }

    void AddLog(string line)
    {
        if (logContent == null) return;

        string timestamp = System.DateTime.Now.ToString("HH:mm:ss");

        // Create a new text entry
        GameObject entry = new GameObject("LogEntry");
        entry.transform.SetParent(logContent, false);

        Text text = entry.AddComponent<Text>();
        text.text = $"<color=#666666>{timestamp}</color> {line}";
        text.font = font != null ? font : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        text.fontSize = Mathf.RoundToInt(fontSize);
        text.color = new Color(0.85f, 0.85f, 0.85f);
        text.supportRichText = true;
        text.horizontalOverflow = HorizontalWrapMode.Wrap;
        text.verticalOverflow = VerticalWrapMode.Overflow;

        // Fit width to parent, height to text
        ContentSizeFitter fitter = entry.AddComponent<ContentSizeFitter>();
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        LayoutElement layout = entry.AddComponent<LayoutElement>();
        layout.flexibleWidth = 1;

        logEntries.Add(entry);

        // Trim old entries
        while (logEntries.Count > maxLogLines)
        {
            Destroy(logEntries[0]);
            logEntries.RemoveAt(0);
        }

        // Auto-scroll to bottom
        Canvas.ForceUpdateCanvases();
        if (scrollRect != null)
            scrollRect.verticalNormalizedPosition = 0f;
    }

    // ── HELPERS ──────────────────────────────────────────────────────────
    string StripUnityColorTags(string input)
    {
        string result = System.Text.RegularExpressions.Regex.Replace(input, @"<color=[^>]*>", "");
        result = result.Replace("</color>", "");
        return result;
    }

    string ColorizeKnownTags(string input)
    {
        foreach (var kvp in tagColors)
        {
            if (input.Contains(kvp.Key))
                return $"<color={kvp.Value}>{input}</color>";
        }
        return input;
    }
}