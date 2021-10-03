using Latios;
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
    public class PlayerDieOnCollisionSystem : SubSystem
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
            // var playerByNetworkId = new NativeHashMap<int, Entity>(50, Allocator.TempJob);
            // Entities.ForEach((in NetworkIdComponent networkId, in CommandTargetComponent target) =>
            // {
            //     playerByNetworkId[networkId.Value] = target.targetEntity;
            // }).Run();
            
            Dependency = new CollisionEventJob
                {

                    DestroyerPerEntity = GetComponentDataFromEntity<PlayerDestroyerTag>(), 
                    GhostOwnersPerEntity = GetComponentDataFromEntity<GhostOwnerComponent>(),
                    HealthPerEntity = GetComponentDataFromEntity<HealthComponent>(),
                }
                .Schedule
                (
                    _stepPhysicsWorld.Simulation,
                    ref _buildPhysicsWorld.PhysicsWorld,
                    Dependency
                );
            
            latiosWorld.syncPoint.AddJobHandleForProducer(Dependency);
            _endSimulationEcbSystem.AddJobHandleForProducer(Dependency);
            // playerByNetworkId.Dispose(Dependency);
        }
        
        [BurstCompile]
        struct CollisionEventJob : ICollisionEventsJob
        {
            // public EntityCommandBuffer Ecb;
            [ReadOnly] public ComponentDataFromEntity<PlayerDestroyerTag> DestroyerPerEntity;
            [ReadOnly] public ComponentDataFromEntity<GhostOwnerComponent> GhostOwnersPerEntity;
            public ComponentDataFromEntity<HealthComponent> HealthPerEntity;

            public void Execute(CollisionEvent e)
            {
                var destroyerEntity = DestroyerPerEntity.GetEntityFromEvent(e);
                var healthEntity = HealthPerEntity.GetEntityFromEvent(e);

                if (healthEntity == Entity.Null || destroyerEntity == Entity.Null)
                    return;

                if (!GhostOwnersPerEntity.HasComponent(healthEntity))
                    return; // not a player

                var newHealth = HealthPerEntity[healthEntity];
                newHealth.Value = 0;
                HealthPerEntity[healthEntity] = newHealth;
            }
        }
    }
}