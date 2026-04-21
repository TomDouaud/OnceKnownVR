using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(NavMeshAgent), typeof(Animator))]
public class GuideController : MonoBehaviour
{
    public ThinkingBubble thinkingBubble;

    public enum GuideState { Wandering = 1, Following = 2}
    public GuideState currentState = GuideState.Following;

    [Header("References")] public Transform player;
    public float wanderRadius = 15f; // Rayon de recherche du point
    public float wanderTimer = 5f; // Temps avant de changer de point

    private NavMeshAgent agent;
    private Animator animator;
    private float timer;
    private bool bLockState = false;

    void Start()
    {
        agent = GetComponent<NavMeshAgent>();
        animator = GetComponent<Animator>();
        timer = wanderTimer;

    }

    void OnEnable()
    {
        // Subscribe to the event
        if (TTSService.Instance != null)
            TTSService.Instance.OnTTSEvent += HandleTTSEvent;

        if (LLMService.Instance != null)
            LLMService.Instance.OnResponseComplete += HandleLLMResponseComplete;
    }

    void OnDisable()
    {
        // Unsubscribe to avoid memory leaks
        if (TTSService.Instance != null)
            TTSService.Instance.OnTTSEvent -= HandleTTSEvent;

        if (LLMService.Instance != null)
            LLMService.Instance.OnResponseComplete -= HandleLLMResponseComplete;
    }

    private void HandleTTSEvent(object sender, TTSEventArgs e)
    {
        if (e.CurrentPhase == TTSEventArgs.Phase.ChunkReady)
        {
            SetThinking(false);
        }

        if (e.CurrentPhase == TTSEventArgs.Phase.Complete)
        {
            Debug.Log("TTS has finished speaking the entire sequence!");
            ChangeLockState(false);
        }
    }

    public void SetThinking(bool thinking)
    {
        if (animator != null)
        {
            animator.SetBool("isThinking", thinking);
        }
    }

    public void PlayGoodbyeAnimation()
    {
        animator.SetTrigger("Goodbye");
    }

    public void ChangeLockState(bool lockState)
    {
        bLockState = lockState;
    }

    void Update()
    {
        switch (currentState)
        {
            case GuideState.Following:
                FollowPlayer();
                break;
            case GuideState.Wandering:
                WanderAround();
                break;
        }

        animator.SetFloat("Speed", agent.velocity.magnitude);
    }

    private void FollowPlayer()
    {
        if (player != null)
        {
            agent.SetDestination(player.position);
            agent.stoppingDistance = 2.0f;
        }
    }

    private void WanderAround()
    {

        timer += Time.deltaTime;

        if (timer >= wanderTimer)
        {
            Vector3 newPos = RandomNavMeshLocation(wanderRadius);
            agent.SetDestination(newPos);
            timer = 0;
        }
    }

    // Fonction pour trouver une position aléatoire sur le NavMesh
    public Vector3 RandomNavMeshLocation(float radius)
    {
        Vector3 randomDirection = Random.insideUnitSphere * radius;
        randomDirection += transform.position;
        NavMeshHit hit;
        NavMesh.SamplePosition(randomDirection, out hit, radius, 1);
        return hit.position;
    }

    // Fonction appelée par l'Animation Event de Walk_N
    public void OnFootstep(AnimationEvent animationEvent)
    {
        // TODO add sfx
    }

    private void HandleLLMResponseComplete(object sender, LLMCompleteEventArgs e)
    {
        // Si la réponse est un succès, on arrête de réfléchir et on commence à parler
        if (e.Success)
        {
            Debug.Log("LLM a répondu, le guide peut parler.");
            StartTalk();
        }
        else
        {
            // Optionnel : Gérer l'erreur (ex: cacher la bulle quand même)
            thinkingBubble.StopThinking();
            Debug.LogError("Erreur LLM : " + e.Error);
        }
    }

    public void StartTalk()
    {
        thinkingBubble.StopThinking();
    }

    public void ChangeState(int newState)
    {
        animator.SetTrigger("Goodbye");
        Debug.Log("Lancement de l'anim Goodbye via ChangeState");

        if (!bLockState)
        {
            currentState = (GuideState)newState;
        }
    }
}
