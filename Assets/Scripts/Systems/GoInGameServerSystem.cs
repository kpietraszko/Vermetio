using System;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.NetCode;
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

            var boatPrefab = GetGhostPrefab<ProbyBuoyantComponent>();
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
                    player = ecb.Instantiate(boatPrefab);
                    var y = GetComponent<Translation>(boatPrefab).Value.y;
                    var randomPosition = rnd.NextFloat3(new float3(-215, y, -331), new float3(280, y, -107)); //new float3(rnd.NextFloat(-215, 280), y, rnd.NextFloat(-331, -107));
                    // Debug.Log($"{randomPosition}");
                    ecb.SetComponent(player, new Translation() {Value = randomPosition});
                    ecb.SetComponent(player, new GhostOwnerComponent { NetworkId = networkIdFromEntity[reqSrc.SourceConnection].Value});
                    ecb.AddBuffer<BoatKeyboardInput>(player);
                    ecb.AddBuffer<BoatMouseInput>(player);
                    ecb.SetComponent(reqSrc.SourceConnection, new CommandTargetComponent {targetEntity = player});
                    ecb.DestroyEntity(reqEnt);
                }).Run();
            
            ecb.Playback(EntityManager);
            
            #if UNITY_EDITOR
            Entities.WithoutBurst().WithNone<Prefab>().ForEach((Entity entity, GhostOwnerComponent owner) =>
            {
                EntityManager.SetName(player, $"PlayerBoat{owner.NetworkId}");
                Debug.Log("Set name");
            }).Run();
            #endif
        }

        private Entity GetGhostPrefab<T>() where T : struct
        {
            var ghostCollection = GetSingletonEntity<GhostPrefabCollectionComponent>();
            var prefabs = EntityManager.GetBuffer<GhostPrefabBuffer>(ghostCollection);
            for (int ghostId = 0; ghostId < prefabs.Length; ++ghostId)
            {
                if (EntityManager.HasComponent<T>(prefabs[ghostId].Value))
                    return prefabs[ghostId].Value;
            }

            return Entity.Null;
        }
    }
}