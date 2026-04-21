using UnityEngine;
using UnityEngine.Events;

public class WaveGestureTrigger : MonoBehaviour
{
    [Header("VR Transforms")]
    public Transform headTransform;
    public Transform handTransform;

    [Header("Wave Settings")]
    [Tooltip("Hauteur min de la main par rapport à la tête")]
    public float heightOffset = -0.2f;
    [Tooltip("Vitesse latérale minimale pour compter un mouvement")]
    public float velocityThreshold = 1.5f;
    [Tooltip("Nombre d'allers-retours nécessaires pour valider")]
    public int requiredSwings = 3;
    [Tooltip("Temps maximum autorisé entre chaque mouvement")]
    public float timeWindow = 1.0f;

    [Header("Events")]
    public UnityEvent OnWavePerformed;

    private Vector3 previousHandPosition;
    private int currentSwings = 0;
    private float gestureTimer = 0f;
    
    // 1 pour droite, -1 pour gauche, 0 pour neutre
    private int lastDirection = 0; 

    void Start()
    {
        if (handTransform != null)
        {
            previousHandPosition = handTransform.position;
        }
    }

    void Update()
    {
        if (headTransform == null || handTransform == null) return;
        
        bool isHandRaised = handTransform.position.y > (headTransform.position.y + heightOffset);

        if (isHandRaised)
        {
            gestureTimer += Time.deltaTime;

            // Si l'utilisateur s'arrête en plein milieu du geste, on réinitialise
            if (gestureTimer > timeWindow)
            {
                ResetGesture();
            }
            
            Vector3 velocity = (handTransform.position - previousHandPosition) / Time.deltaTime;
            
            float horizontalMovement = Vector3.Dot(velocity, headTransform.right);
            
            if (Mathf.Abs(horizontalMovement) > velocityThreshold)
            {
                int currentDirection = (int)Mathf.Sign(horizontalMovement);
                
                if (lastDirection != 0 && currentDirection != lastDirection)
                {
                    currentSwings++;
                    gestureTimer = 0f; 
                    
                    if (currentSwings >= requiredSwings)
                    {
                        TriggerEvent();
                        ResetGesture(); // Reset pour éviter de spammer l'event
                    }
                }
                lastDirection = currentDirection;
            }
        }
        else
        {
            
            ResetGesture();
        }
        
        previousHandPosition = handTransform.position;
    }

    private void ResetGesture()
    {
        currentSwings = 0;
        gestureTimer = 0f;
        lastDirection = 0;
    }

    private void TriggerEvent()
    {
        if (OnWavePerformed != null)
        {
            OnWavePerformed.Invoke();
        }
    }
}