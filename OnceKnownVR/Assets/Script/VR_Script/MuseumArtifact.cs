using UnityEngine;
using System.Collections.Generic;

public class MuseumArtifact : MonoBehaviour
{
    [Header("Identifiants")]
    public string artifactId;
    public string artifactName;

    [Header("Réglages du Glow")]
    public bool enableHighlight = true;
    public Color glowColor = Color.white;
    [Range(0f, 5f)] public float intensity = 0.1f; // Réglé à 1.2 pour ne pas tout "brûler"

    private MeshRenderer[] allRenderers;
    private Dictionary<Material, Color> originalEmissionColors = new Dictionary<Material, Color>();

    void Start()
    {
        allRenderers = GetComponentsInChildren<MeshRenderer>();
        // On pré-cache les matériaux et leurs émissions
        foreach (MeshRenderer ren in allRenderers)
        {
            foreach (Material mat in ren.materials)
            {
                if (mat.HasProperty("_EmissionColor"))
                {
                    originalEmissionColors[mat] = mat.GetColor("_EmissionColor");
                }
            }
        }
    }

    public void OnHoverStart()
    {
        if (!enableHighlight) return;

        foreach (MeshRenderer ren in allRenderers)
        {
            foreach (Material mat in ren.materials)
            {
                mat.EnableKeyword("_EMISSION");

                // On crée un blanc doux. 
                // Pour l'opacité 50%, on réduit l'intensité au lieu de l'alpha 
                // car l'émission est additive sur le shader standard.
                Color finalGlow = glowColor * intensity * 0.5f;

                mat.SetColor("_EmissionColor", finalGlow);
            }
        }
    }

    public void OnHoverEnd()
    {
        foreach (MeshRenderer ren in allRenderers)
        {
            foreach (Material mat in ren.materials)
            {
                if (originalEmissionColors.ContainsKey(mat))
                {
                    mat.SetColor("_EmissionColor", originalEmissionColors[mat]);
                }
                else
                {
                    mat.DisableKeyword("_EMISSION");
                }
            }
        }
    }
}