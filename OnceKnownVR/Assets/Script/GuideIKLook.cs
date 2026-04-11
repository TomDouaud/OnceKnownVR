using UnityEngine;

namespace Script
{
    public class GuideIKLook : MonoBehaviour
    {
        public GuideController controller;

        [Header("Réglages de l'IK")]
        [Range(0, 5)] public float transitionSpeed = 2f; // Vitesse de la transition
        public float bodyWeight = 0.2f;
        public float headWeight = 0.8f;

        private Animator animator;
        private float currentWeight = 0f; // Le poids réel appliqué
        private float targetWeight = 0f;  // Le poids que l'on veut atteindre

        void Start()
        {
            animator = GetComponent<Animator>();
        }

        void Update()
        {
            if (controller == null) return;

            if (controller.currentState == GuideController.GuideState.Following && controller.player != null)
            {
                targetWeight = 1f;
            }
            else
            {
                targetWeight = 0f;
            }
            currentWeight = Mathf.MoveTowards(currentWeight, targetWeight, Time.deltaTime * transitionSpeed);
        }

        void OnAnimatorIK(int layerIndex)
        {
            if (animator == null || controller == null) return;
            animator.SetLookAtWeight(currentWeight, bodyWeight, headWeight);

            if (controller.player != null)
            {
                animator.SetLookAtPosition(controller.player.position);
            }
        }
    }
}