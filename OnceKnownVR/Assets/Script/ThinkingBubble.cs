using UnityEngine;
using TMPro; // Requis pour TextMeshPro
using System.Collections;

public class ThinkingBubble : MonoBehaviour
{
    [Header("UI Elements")]
    public GameObject bubbleContainer; // Le GameObject qui contient l'Image de la bulle
    public TextMeshProUGUI dotsText;   // Le texte à l'intérieur

    [Header("Settings")]
    public float animationSpeed = 0.4f; // Vitesse d'apparition des points

    private Coroutine thinkingCoroutine;

    void Start()
    {
        // On cache la bulle au démarrage
        bubbleContainer.SetActive(false);
    }

    // Appelle cette fonction quand ton IA commence à réfléchir
    public void StartThinking()
    {
        bubbleContainer.SetActive(true);
        if (thinkingCoroutine == null)
        {
            thinkingCoroutine = StartCoroutine(AnimateDots());
        }
    }

    // Appelle cette fonction quand l'IA a trouvé sa réponse
    public void StopThinking()
    {
        bubbleContainer.SetActive(false);
        if (thinkingCoroutine != null)
        {
            StopCoroutine(thinkingCoroutine);
            thinkingCoroutine = null;
        }
    }

    private IEnumerator AnimateDots()
    {
        int dotCount = 1;
        
        while (true)
        {
            // Construit la chaîne avec le bon nombre de points
            dotsText.text = new string('.', dotCount);

            dotCount++;
            if (dotCount > 3) 
            {
                dotCount = 1; // On recommence à 1 point
            }

            // Attend avant de changer le texte
            yield return new WaitForSeconds(animationSpeed);
        }
    }
}