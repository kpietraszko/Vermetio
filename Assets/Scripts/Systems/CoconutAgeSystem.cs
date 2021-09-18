using System.Collections.Generic;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.NetCode;
using Unity.Rendering;
using Unity.Transforms;

namespace Vermetio.Server
{
    [UpdateInGroup(typeof(GhostSimulationSystemGroup))]
    [UpdateInWorld(UpdateInWorld.TargetWorld.Server)]
    public class CoconutAgeSystem : SystemBase
    {
        private EndSimulationEntityCommandBufferSystem _endSimulationEcbSystem;
        private CoconutSpawnSystem _coconutSpawnSystem;
        
        protected override void OnCreate()
        {
            base.OnCreate();
            _endSimulationEcbSystem = World.GetOrCreateSystem<EndSimulationEntityCommandBufferSystem>();
            _coconutSpawnSystem = World.GetExistingSystem<CoconutSpawnSystem>();
        }

        private const float MaxAge = 20;

        protected override void OnUpdate()
        {
            var deltaTime = Time.DeltaTime;
            var endFrameEcb = _endSimulationEcbSystem.CreateCommandBuffer();
            var existingCoconutsQuery = _coconutSpawnSystem._existingCoconutsQuery;
            var existingCoconuts = existingCoconutsQuery.CalculateEntityCount();
            var maxCoconuts = CoconutSpawnSystem.TargetNumberOfCoconuts;
            var largestAge = 0.0;
            var largestAgeEntity = Entity.Null;

            if (existingCoconuts > maxCoconuts)
            {
                Entities.WithNone<BulletTag>().ForEach((Entity entity, in CoconutAgeComponent age) => // finds oldest coconut
                {
                    if (age.Age > largestAge)
                    {
                        largestAge = age.Age;
                        largestAgeEntity = entity;
                    }
                }).Run();
                
                endFrameEcb.DestroyEntity(largestAgeEntity); // destroys oldest coconut
            }

            Entities.ForEach((ref CoconutAgeComponent age) =>
            {
                age.Age += deltaTime;
            }).Schedule();

            _endSimulationEcbSystem.AddJobHandleForProducer(Dependency);
        }
    }
}
