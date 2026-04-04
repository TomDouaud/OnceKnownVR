using System.Text;
using UnityEngine.Networking;

// ════════════════════════════════════════════════════════════════════════════
//  Reusable SSE streaming download handler for Unity web requests.
//  Used by LLMService (and potentially future streaming endpoints).
// ════════════════════════════════════════════════════════════════════════════

public class StreamingDownloadHandler : DownloadHandlerScript
{
    private readonly StringBuilder fullText = new StringBuilder();
    private int lastReadIndex = 0;

    protected override bool ReceiveData(byte[] data, int dataLength)
    {
        string text = Encoding.UTF8.GetString(data, 0, dataLength);
        fullText.Append(text);
        return true;
    }
    
    /// Returns only the data received since the last call (incremental read).
    public string ConsumeNewData()
    {
        if (fullText.Length <= lastReadIndex) return null;
        string newData = fullText.ToString(lastReadIndex, fullText.Length - lastReadIndex);
        lastReadIndex = fullText.Length;
        return newData;
    }
    
    /// Returns the full accumulated text after SSE parsing
    /// (strips "data: " prefixes, skips [DONE] and [STATUS] lines).
    public string GetFullText()
    {
        StringBuilder clean = new StringBuilder();
        string[] lines = fullText.ToString().Split('\n');
        foreach (string line in lines)
        {
            string trimmed = line.Trim();
            if (trimmed.StartsWith("data: "))
            {
                string token = trimmed.Substring(6);
                if (token == "[DONE]")            continue;
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
    
    public static string ParseSSEChunk(string raw)
    {
        StringBuilder clean = new StringBuilder();
        string[] lines = raw.Split('\n');
        foreach (string line in lines)
        {
            string trimmed = line.Trim();
            if (trimmed.StartsWith("data: "))
            {
                string token = trimmed.Substring(6);
                if (token == "[DONE]")             continue;
                if (token.StartsWith("[STATUS]"))  continue;  // ← add this
                if (token.StartsWith("[ERROR]"))   continue;  // ← and this
                clean.Append(token);
            }
        }
        return clean.ToString();
    }

    protected override void CompleteContent() { }
    protected override float GetProgress() => 0;
}
