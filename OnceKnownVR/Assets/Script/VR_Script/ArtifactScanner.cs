using UnityEngine;

// ════════════════════════════════════════════════════════════════════════════
// Attacher à la manette VR droite. Utilise un LineRenderer pour le laser.
// ════════════════════════════════════════════════════════════════════════════
[RequireComponent(typeof(LineRenderer))]
public class VRArtifactScanner : MonoBehaviour
{
    [Header("Paramètres du Raycast")]
    public float rayLength = 10f;
    
    [Tooltip("Pour optimiser, mets tes œuvres sur un Layer 'Interactable' et sélectionne-le ici")]
    public LayerMask interactableLayer;

    /// <summary>The artifactId of whatever the laser is currently pointing at. Empty if nothing.</summary>
    public string CurrentArtifactId { get; private set; } = "";

    private LineRenderer laserRenderer;
    private MuseumArtifact currentTarget;

    void Start()
    {
        laserRenderer = GetComponent<LineRenderer>();
        
        // Configuration rapide du design du laser si ce n'est pas fait dans l'éditeur
        laserRenderer.startWidth = 0.01f;
        laserRenderer.endWidth = 0.01f;
        laserRenderer.material = new Material(Shader.Find("Sprites/Default"));
        laserRenderer.startColor = new Color(1, 1, 1, 0.5f); // Blanc semi-transparent
        laserRenderer.endColor = new Color(1, 1, 1, 0f);     // Disparaît au bout
    }

    void Update()
    {
        // 1. Le début du laser part de la manette
        laserRenderer.SetPosition(0, transform.position);

        RaycastHit hit;
        // 2. On tire le rayon droit devant (transform.forward)
        if (Physics.Raycast(transform.position, transform.forward, out hit, rayLength, interactableLayer))
        {
            // Si on touche quelque chose, on arrête le visuel du laser sur l'objet
            laserRenderer.SetPosition(1, hit.point);

            // On vérifie si l'objet touché a notre script d'œuvre
            MuseumArtifact artifact = hit.collider.GetComponent<MuseumArtifact>();

            if (artifact != null)
            {
                // Si on vient de cibler une NOUVELLE œuvre
                if (currentTarget != artifact)
                {
                    if (currentTarget != null) currentTarget.OnHoverEnd(); // Désélectionne l'ancienne
                    
                    currentTarget = artifact;
                    currentTarget.OnHoverStart();
                    CurrentArtifactId = currentTarget.artifactId;
                    Debug.Log($"<color=cyan>[SCANNER] Œuvre ciblée : {currentTarget.artifactName}</color>");
                }
            }
        }
        else
        {
            // Si on ne touche rien, le laser va à sa distance max
            laserRenderer.SetPosition(1, transform.position + transform.forward * rayLength);

            // On réinitialise si on regardait une œuvre avant
            if (currentTarget != null)
            {
                currentTarget.OnHoverEnd();
                currentTarget = null;
                CurrentArtifactId = "";
                
                Debug.Log($"<color=cyan>[SCANNER] Œuvre ciblée : Aucune</color>");
            }
        }
    }
}