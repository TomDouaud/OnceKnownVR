using UnityEngine;

public class MuseumArtifact : MonoBehaviour
{
    [Header("Identifiants pour le LLM")]
    [Tooltip("L'ID exact que l'IA Python/Node va reconnaître (ex: vase_ming, joconde)")]
    public string artifactId;
    
    [Tooltip("Le nom affiché (pour le debug ou une future UI)")]
    public string artifactName;

    [Header("Feedback Visuel")]
    public bool enableHighlight = true;
    
    private Renderer meshRenderer;
    private Color originalColor;

    void Start()
    {
        meshRenderer = GetComponent<Renderer>();
        if (meshRenderer != null)
        {
            originalColor = meshRenderer.material.color;
        }
    }
    
    public void OnHoverStart()
    {
        if (enableHighlight && meshRenderer != null)
        {
            // On teinte légèrement l'œuvre pour montrer qu'elle est sélectionnée
            meshRenderer.material.color = Color.Lerp(originalColor, Color.yellow, 0.3f);
        }
    }
    
    public void OnHoverEnd()
    {
        if (enableHighlight && meshRenderer != null)
        {
            meshRenderer.material.color = originalColor;
        }
    }
}