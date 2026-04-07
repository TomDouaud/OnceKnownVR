using System;
using Unity.Mathematics;
using UnityEngine;

public class LookAtPlayer : MonoBehaviour
{
    [Header("References")]
    public GuideController controller;
    
    private quaternion initialRotation;
    void Start()
    {
        initialRotation = transform.rotation;
    }

    // Update is called once per frame
    void LateUpdate()
    {
        switch (controller.currentState)
        {
            case GuideController.GuideState.Following:
                transform.LookAt(controller.player);
                break;
            case GuideController.GuideState.Wandering:
                Vector3 direction = (controller.player.position - transform.position).normalized;
                Quaternion lookRotation = Quaternion.LookRotation(new Vector3(direction.x, 0, direction.z));
                transform.rotation *= Quaternion.Slerp(transform.rotation, lookRotation, Time.deltaTime * 5f);

                break;
        }
    }
}
