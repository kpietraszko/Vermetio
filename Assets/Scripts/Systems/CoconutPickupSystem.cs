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
    [UpdateInGroup(typeof(GhostSimulationSystemGroup))]
    [UpdateInWorld(UpdateInWorld.TargetWorld.Server)]
    [AlwaysUpdateSystem]
    public class CoconutPickupSystem : SystemBase
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
            var elapsedTime = Time.ElapsedTime;
            var endFrameEcb = _endSimulationEcbSystem.CreateCommandBuffer();

            Entities
                .ForEach((Entity entity, int entityInQueryIndex, CoconutPickupCooldownComponent cooldown) =>
                {
                    if (cooldown.CooldownStartedAt + 1.0 <= elapsedTime) // 1 second cooldown
                        endFrameEcb.RemoveComponent<CoconutPickupCooldownComponent>(entity);
                }).Run();
            
            Dependency = new CollisionEventJob
                {
                    CoconutsPerEntity = GetComponentDataFromEntity<CoconutAgeComponent>(),
                    InventoriesPerEntity = GetComponentDataFromEntity<PlayerInventoryComponent>(), 
                    BulletTagsPerEntity = GetComponentDataFromEntity<BulletTag>(),
                    PickupCooldownsPerEntity = GetComponentDataFromEntity<CoconutPickupCooldownComponent>(), 
                    Ecb = endFrameEcb,
                    ElapsedTime = elapsedTime
                }
                .Schedule
                (
                    _stepPhysicsWorld.Simulation,
                    ref _buildPhysicsWorld.PhysicsWorld,
                    Dependency
                );
            
            _endSimulationEcbSystem.AddJobHandleForProducer(Dependency);
            _buildPhysicsWorld.AddInputDependencyToComplete(Dependency);
        }
        
        [BurstCompile]
        struct CollisionEventJob : ICollisionEventsJob
        {
            public ComponentDataFromEntity<PlayerInventoryComponent> InventoriesPerEntity;
            [ReadOnly] public ComponentDataFromEntity<CoconutAgeComponent> CoconutsPerEntity;
            [ReadOnly] public ComponentDataFromEntity<BulletTag> BulletTagsPerEntity;
            [ReadOnly] public ComponentDataFromEntity<CoconutPickupCooldownComponent> PickupCooldownsPerEntity;
            public EntityCommandBuffer Ecb;
            public double ElapsedTime;

            public void Execute(CollisionEvent e)
            {
                if (CoconutsPerEntity.HasComponent(e.EntityA) && InventoriesPerEntity.HasComponent(e.EntityB))
                {
                    if (PickupCooldownsPerEntity.HasComponent(e.EntityB))
                        return;
                    
                    if (BulletTagsPerEntity.HasComponent(e.EntityA)) // can't pick up bullets
                        return;
                    
                    var newInv = InventoriesPerEntity[e.EntityB];
                    newInv.Coconuts++;
                    InventoriesPerEntity[e.EntityB] = newInv;
                    Ecb.DestroyEntity(e.EntityA);
                    Ecb.AddComponent(e.EntityB, new CoconutPickupCooldownComponent() { CooldownStartedAt = ElapsedTime});
                }
                
                if (CoconutsPerEntity.HasComponent(e.EntityB) && InventoriesPerEntity.HasComponent(e.EntityA))
                {
                    if (PickupCooldownsPerEntity.HasComponent(e.EntityA))
                        return;
                    
                    if (BulletTagsPerEntity.HasComponent(e.EntityB)) // can't pick up bullets
                        return;
                    
                    var newInv = InventoriesPerEntity[e.EntityA];
                    newInv.Coconuts++;
                    InventoriesPerEntity[e.EntityA] = newInv;
                    Ecb.DestroyEntity(e.EntityB);
                    Ecb.AddComponent(e.EntityA, new CoconutPickupCooldownComponent() { CooldownStartedAt = ElapsedTime});
                }
            }
        }
    }
}