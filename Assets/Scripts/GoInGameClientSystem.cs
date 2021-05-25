using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.NetCode;
using Unity.Transforms;
using UnityEngine;

[UpdateInGroup(typeof(ClientSimulationSystemGroup))]
public class GoInGameClientSystem : SystemBase
{

    protected override void OnCreate()
    {
        RequireSingletonForUpdate<GhostPrefabCollectionComponent>();
        RequireForUpdate(GetEntityQuery(ComponentType.ReadOnly<NetworkIdComponent>(), ComponentType.Exclude<NetworkStreamInGame>()));
    }

    protected override void OnUpdate()
    {
        var ecb = World.GetOrCreateSystem<BeginSimulationEntityCommandBufferSystem>().CreateCommandBuffer();
        
        var prespawnCount = EntityManager.CreateEntityQuery(ComponentType.ReadOnly<PreSpawnedGhostId>()).CalculateEntityCount();
        var buoyantCount = EntityManager.CreateEntityQuery(ComponentType.ReadOnly<BuoyantComponent>()).CalculateEntityCount();
        
        Entities.WithNone<NetworkStreamInGame>().ForEach((Entity ent, ref NetworkIdComponent id) =>
        {
            if (prespawnCount != 1)
                return;

            Debug.Log($"{buoyantCount} buoyant components");
            
            ecb.AddComponent<NetworkStreamInGame>(ent);
            var req = ecb.CreateEntity();
            ecb.AddComponent<ConnectionSystem.GoInGameRequest>(req);
            ecb.AddComponent(req, new SendRpcCommandRequestComponent { TargetConnection = ent });
        }).Run();
       
    }
}
