using Cinemachine;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.NetCode;
using Unity.Transforms;
using UnityEngine;

[UpdateInGroup(typeof(ClientSimulationSystemGroup))]
[UpdateAfter(typeof(SyncPlayerPositionToProxySystem))]
public class AssignPlayerProxyToVirtualCamSystem : SystemBase
{
    protected override void OnUpdate()
    {
        Transform playerProxyTransform = null;
        if (!TryGetSingletonEntity<PlayerProxyTagComponent>(out var playerProxyTagEntity))
            return;

        playerProxyTransform = EntityManager.GetComponentObject<DummyProxyHybridAuthoring>(playerProxyTagEntity)?.transform; // if this doesn't work, try getting it through companionlink

        if (playerProxyTransform == null)
            return;
        
        Entities
            .WithoutBurst()
            .ForEach((CinemachineVirtualCamera virtualCam) => {
            Debug.Log("Found virtual cam!");
            if (virtualCam.Follow == null)
            {
                virtualCam.Follow = playerProxyTransform;
            }

            if (virtualCam.LookAt == null)
            {
                virtualCam.LookAt = playerProxyTransform;
            }

            virtualCam.Priority = 1000;
            }).Run();
    }
}
