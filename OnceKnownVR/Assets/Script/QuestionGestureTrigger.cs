using UnityEngine;
using UnityEngine.Events;

public class QuestionGestureTrigger : MonoBehaviour
{
    [Header("VR Transforms")]
    public Transform headTransform;
    public Transform leftHandTransform;
    public Transform rightHandTransform;

    [Header("Gesture Settings")]
    [Tooltip("La distance minimale au-dessus de la tête pour valider le geste")]
    public float heightThreshold = 0.2f; 
    [Tooltip("Temps en secondes pendant lequel il faut maintenir la pose")]
    public float requiredHoldTime = 5.0f;

    [Header("Events")]
    public UnityEvent OnGesturePerformed;

    private float currentHoldTime = 0f;
    private bool gestureTriggered = false;

    void Update()
    {
        if (CheckGestureConditions())
        {
            currentHoldTime += Time.deltaTime;

            if (currentHoldTime >= requiredHoldTime && !gestureTriggered)
            {
                TriggerEvent();
            }
        }
        else
        {
            // Reset
            currentHoldTime = 0f;
            gestureTriggered = false; 
        }
    }

    private bool CheckGestureConditions()
    {
        bool isLeftHandUp = leftHandTransform.position.y > (headTransform.position.y + heightThreshold);
        bool isRightHandUp = rightHandTransform.position.y > (headTransform.position.y + heightThreshold);

        return isLeftHandUp || isRightHandUp;
    }

    private void TriggerEvent()
    {
        gestureTriggered = true;
        
        if (OnGesturePerformed != null)
        {
            OnGesturePerformed.Invoke();
            Debug.Log("Geste reconnu ! Signal envoyé à l'IA.");
        }
    }
}