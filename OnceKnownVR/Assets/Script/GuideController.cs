using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(NavMeshAgent), typeof(Animator))]
public class GuideController : MonoBehaviour
{
    public enum GuideState { Wandering, Following }
    public GuideState currentState = GuideState.Following;

    [Header("References")]
    public Transform player;
    public Transform[] museumWaypoints; // Points de passage pour la balade

    private NavMeshAgent agent;
    private Animator animator;
    private int currentWaypointIndex = 0;

    void Start()
    {
        agent = GetComponent<NavMeshAgent>();
        animator = GetComponent<Animator>();
    }

    void Update()
    {
        // 1. Gérer la destination selon l'état
        switch (currentState)
        {
            case GuideState.Following:
                FollowPlayer();
                break;
            case GuideState.Wandering:
                WanderAround();
                break;
        }

        // 2. Synchroniser l'animation avec la vitesse réelle de l'agent
        // agent.velocity.magnitude renvoie la vitesse de déplacement exacte
        animator.SetFloat("Speed", agent.velocity.magnitude);
    }

    private void FollowPlayer()
    {
        if (player != null)
        {
            agent.SetDestination(player.position);
            // Optionnel : Arrêter l'agent s'il est assez proche du joueur
            agent.stoppingDistance = 2.0f; 
        }
    }

    private void WanderAround()
    {
        agent.stoppingDistance = 0f; // Il doit aller jusqu'au point
        
        if (museumWaypoints.Length == 0) return;

        // Si l'agent est arrivé à destination, on passe au point suivant
        if (!agent.pathPending && agent.remainingDistance <= agent.stoppingDistance)
        {
            currentWaypointIndex = (currentWaypointIndex + 1) % museumWaypoints.Length;
            agent.SetDestination(museumWaypoints[currentWaypointIndex].position);
        }
    }
    
    // Fonction appelée par l'Animation Event de Walk_N
    public void OnFootstep(AnimationEvent animationEvent)
    {
        // On laisse ça vide pour faire taire l'erreur !
        // Plus tard, on pourra y coder un système de son pour les pas.
    }
}