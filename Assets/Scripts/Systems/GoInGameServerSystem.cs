using System;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.NetCode;
using Unity.Physics;
using Unity.Physics.GraphicsIntegration;
using Unity.Transforms;
using UnityEngine;

namespace Vermetio.Server
{
    [UpdateInGroup(typeof(ServerSimulationSystemGroup))]
    public class GoInGameServerSystem : SystemBase
    {
        protected override void OnCreate()
        {
            base.OnCreate();
            RequireForUpdate(GetEntityQuery(ComponentType.ReadOnly<ConnectionSystem.GoInGameRequest>(), ComponentType.ReadOnly<ReceiveRpcCommandRequestComponent>()));
        }

        protected override void OnUpdate()
        {
            var ecb = new EntityCommandBuffer(Allocator.Temp);

            var boatPrefab = EntityHelpers.GetGhostPrefab<ProbyBuoyantComponent>(EntityManager);
            var networkIdFromEntity = GetComponentDataFromEntity<NetworkIdComponent>(true);
            var rnd = new Unity.Mathematics.Random((uint)DateTime.Now.Millisecond * 1500000000u);
            Entity player = Entity.Null;

            Entities
                .WithReadOnly(networkIdFromEntity)
                .WithNone<SendRpcCommandRequestComponent>()
                .ForEach((Entity reqEnt, ref ConnectionSystem.GoInGameRequest req,
                    ref ReceiveRpcCommandRequestComponent reqSrc) =>
                {
                    ecb.AddComponent<NetworkStreamInGame>(reqSrc.SourceConnection);
                    Debug.Log(String.Format("Server setting connection {0} to in game", GetComponent<NetworkIdComponent>(reqSrc.SourceConnection).Value));
                    if (boatPrefab == null)
                        Debug.LogError("Player boat prefab not found!");
                    
                    player = ecb.Instantiate(boatPrefab);
                    var y = GetComponent<Translation>(boatPrefab).Value.y;
                    var randomPosition = rnd.NextFloat3(new float3(-215, y, -331), new float3(280, y, -107));
                    Debug.Log("Spawned player boat");
                    ecb.SetComponent(player, new Translation() {Value = new float3(-81.6f, y, -167.4f)});
                    ecb.SetComponent(player, new GhostOwnerComponent { NetworkId = networkIdFromEntity[reqSrc.SourceConnection].Value});
                    ecb.AddBuffer<BoatInput>(player);
                    ecb.SetComponent(reqSrc.SourceConnection, new CommandTargetComponent {targetEntity = player});
                    ecb.DestroyEntity(reqEnt);
                }).Run();
            
            ecb.Playback(EntityManager);

            var parentPerEntity = GetComponentDataFromEntity<Parent>(true);
            var ghostOwnerPerEntity = GetComponentDataFromEntity<GhostOwnerComponent>(true);

            // Entities
            //     .WithReadOnly(parentPerEntity)
            //     .WithReadOnly(ghostOwnerPerEntity)
            //     .WithoutBurst()
            //     .ForEach((Entity entity, ref BulletSpawnPointComponent spawnPoint) =>
            //     {
            //         var root = GetRootParent(entity, parentPerEntity);
            //         spawnPoint = new BulletSpawnPointComponent() {NetworkId = ghostOwnerPerEntity[root].NetworkId};
            //     }).Run();
            
            #if UNITY_EDITOR
            Entities.WithoutBurst().WithNone<Prefab>().ForEach((Entity entity, in GhostOwnerComponent owner) =>
            {
                EntityManager.SetName(entity, $"PlayerBoat{owner.NetworkId}");
                Debug.Log("Set name");
            }).Run();
            #endif
        }
    }
}