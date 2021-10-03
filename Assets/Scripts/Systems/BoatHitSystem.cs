using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.NetCode;
using Unity.Physics;
using Unity.Physics.Systems;
using Unity.Transforms;
using UnityEngine;

namespace Vermetio.Server
{
    [UpdateInWorld(UpdateInWorld.TargetWorld.Server)]
    [UpdateAfter(typeof(StepPhysicsWorld))]
    public class BoatHitSystem : SystemBase
    {
        private StepPhysicsWorld _stepPhysicsWorld;
        private BuildPhysicsWorld _buildPhysicsWorld;
        private EndSimulationEntityCommandBufferSystem _endSimulationEcbSystem;

        protected override void OnCreate()
        {
            base.OnCreate();
            _stepPhysicsWorld = World.GetExistingSystem<StepPhysicsWorld>();
            _buildPhysicsWorld = World.GetExistingSystem<BuildPhysicsWorld>();
            _endSimulationEcbSystem = World.GetOrCreateSystem<EndSimulationEntityCommandBufferSystem>();
        }

        protected override void OnUpdate()
        {
            var playerByNetworkId = new NativeHashMap<int, Entity>(50, Allocator.TempJob);
            Entities.ForEach((in NetworkIdComponent networkId, in CommandTargetComponent target) =>
            {
                playerByNetworkId[networkId.Value] = target.targetEntity;
            }).Run();
            
            Dependency = new CollisionEventJob
                {
                    BulletTagsPerEntity = GetComponentDataFromEntity<BulletComponent>(),
                    SpawnedByPerEntity = GetComponentDataFromEntity<SpawnedByComponent>(),
                    GhostOwnersPerEntity = GetComponentDataFromEntity<GhostOwnerComponent>(), 
                    HealthPerEntity = GetComponentDataFromEntity<HealthComponent>(), 
                    PlayerEntityPerNetworkId = playerByNetworkId
                }
                .Schedule
                (
                    _stepPhysicsWorld.Simulation,
                    ref _buildPhysicsWorld.PhysicsWorld,
                    Dependency
                );
            
            playerByNetworkId.Dispose(Dependency);
            _endSimulationEcbSystem.AddJobHandleForProducer(Dependency);
        }
        
        [BurstCompile]
        struct CollisionEventJob : ICollisionEventsJob
        {
            // public EntityCommandBuffer Ecb;
            [ReadOnly] public ComponentDataFromEntity<BulletComponent> BulletTagsPerEntity;
            [ReadOnly] public ComponentDataFromEntity<SpawnedByComponent> SpawnedByPerEntity;
            [ReadOnly] public ComponentDataFromEntity<GhostOwnerComponent> GhostOwnersPerEntity;
            public ComponentDataFromEntity<HealthComponent> HealthPerEntity;
            [ReadOnly] public NativeHashMap<int, Entity> PlayerEntityPerNetworkId;

            public void Execute(CollisionEvent e)
            {
                var bulletEntity = BulletTagsPerEntity.GetEntityFromEvent(e);

                var ghostOwnerEntity = GhostOwnersPerEntity.GetEntityFromEvent(e);
                
                if (bulletEntity == Entity.Null || ghostOwnerEntity == Entity.Null)
                    return;
                
                var playerEntity = PlayerEntityPerNetworkId[GhostOwnersPerEntity[ghostOwnerEntity].NetworkId];
                
                if (SpawnedByPerEntity[bulletEntity].Player != playerEntity) // it's impossible to shoot yourself
                {
                    var newHealth = HealthPerEntity[playerEntity];
                    newHealth.Value -= 1;
                    HealthPerEntity[playerEntity] = newHealth;
                }
            }
        }
    }
}