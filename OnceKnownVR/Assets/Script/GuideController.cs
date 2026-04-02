using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(NavMeshAgent), typeof(Animator))]
public class GuideController : MonoBehaviour
{
    public enum GuideState { Wandering, Following }
    public GuideState currentState = GuideState.Following;

    [Header("References")]
    public Transform player;
    public float wanderRadius = 15f; // Rayon de recherche du point
    public float wanderTimer = 5f;   // Temps avant de changer de point
    
    private NavMeshAgent agent;
    private Animator animator;
    private float timer;
    private int currentWaypointIndex = 0;

    void Start()
    {
        agent = GetComponent<NavMeshAgent>();
        animator = GetComponent<Animator>();
        timer = wanderTimer;
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

    public void PlayerCalled()
    {
        currentState = GuideState.Following;
    }

    public void Wander()
    {
        currentState = GuideState.Wandering;
    }
}