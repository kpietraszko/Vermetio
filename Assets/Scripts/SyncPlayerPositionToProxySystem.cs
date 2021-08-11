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
        var thisClientNetworkId = GetSingleton<NetworkIdComponent>().Value;
        Entities.ForEach((ref Translation translation, in GhostOwnerComponent ghostOwner) => {
            if (ghostOwner.NetworkId == thisClientNetworkId)
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
