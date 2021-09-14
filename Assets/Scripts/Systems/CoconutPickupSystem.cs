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
            Dependency = new CollisionEventJob
                {
                    CoconutsPerEntity = GetComponentDataFromEntity<CoconutAgeComponent>(),
                    InventoriesPerEntity = GetComponentDataFromEntity<PlayerInventoryComponent>(), 
                    Ecb = _endSimulationEcbSystem.CreateCommandBuffer()
                }
                .Schedule
                (
                    _stepPhysicsWorld.Simulation,
                    ref _buildPhysicsWorld.PhysicsWorld,
                    Dependency
                );
            
            _endSimulationEcbSystem.AddJobHandleForProducer(Dependency);
        }
        
        [BurstCompile]
        struct CollisionEventJob : ICollisionEventsJob
        {
            public ComponentDataFromEntity<CoconutAgeComponent> CoconutsPerEntity;
            public ComponentDataFromEntity<PlayerInventoryComponent> InventoriesPerEntity;
            public ComponentDataFromEntity<BulletTag> BulletTagsPerEntity;
            public EntityCommandBuffer Ecb;

            public void Execute(CollisionEvent e)
            {
                if (CoconutsPerEntity.HasComponent(e.EntityA) && InventoriesPerEntity.HasComponent(e.EntityB))
                {
                    if (BulletTagsPerEntity.HasComponent(e.EntityA)) // can't pick up bullets
                        return;
                    
                    var newInv = InventoriesPerEntity[e.EntityB];
                    newInv.Coconuts++;
                    InventoriesPerEntity[e.EntityB] = newInv;
                    Ecb.DestroyEntity(e.EntityA);
                }
                
                if (CoconutsPerEntity.HasComponent(e.EntityB) && InventoriesPerEntity.HasComponent(e.EntityA))
                {
                    if (BulletTagsPerEntity.HasComponent(e.EntityB)) // can't pick up bullets
                        return;
                    
                    var newInv = InventoriesPerEntity[e.EntityA];
                    newInv.Coconuts++;
                    InventoriesPerEntity[e.EntityA] = newInv;
                    Ecb.DestroyEntity(e.EntityB);
                }
            }
        }
    }
}