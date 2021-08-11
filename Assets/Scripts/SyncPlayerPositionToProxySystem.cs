using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.NetCode;
using Unity.Transforms;
using UnityEngine;

[UpdateInGroup(typeof(ClientSimulationSystemGroup))]
public class SyncPlayerPositionToProxySystem : SystemBase
{
    protected override void OnUpdate()
    {
        float3 playerPosition = default;
        if (!TryGetSingleton<NetworkIdComponent>(out var thisClientNetworkId))
            return;
        
        Entities.ForEach((ref Translation translation, in GhostOwnerComponent ghostOwner) => {
            if (ghostOwner.NetworkId == thisClientNetworkId.Value)
            {
                playerPosition = translation.Value;
                // Debug.Log($"Retrieved {playerPosition}");
            }
        }).Run();

        Entities.WithAll<PlayerProxyTagComponent, DummyProxyHybridAuthoring>().ForEach((ref Translation translation) =>
        {
            // Debug.Log($"Applying {playerPosition}");
            translation = new Translation() {Value = playerPosition};
        }).Run();
    }
}
