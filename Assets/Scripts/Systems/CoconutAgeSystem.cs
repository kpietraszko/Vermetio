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
    public class CoconutAgeSystem : SystemBase
    {
        private EndSimulationEntityCommandBufferSystem _endSimulationEcbSystem;
        
        protected override void OnCreate()
        {
            base.OnCreate();
            _endSimulationEcbSystem = World.GetOrCreateSystem<EndSimulationEntityCommandBufferSystem>();
        }

        private const float MaxAge = 20;
        
        protected override void OnUpdate()
        {
            var deltaTime = Time.DeltaTime;
            var endFrameEcb = _endSimulationEcbSystem.CreateCommandBuffer().AsParallelWriter();
            
            Entities.ForEach((ref CoconutAgeComponent age) =>
            {
                age.Age += deltaTime;
            }).Schedule();

            Entities.ForEach((Entity entity, int entityInQueryIndex, in CoconutAgeComponent age) =>
            {
                if (age.Age > MaxAge)
                    endFrameEcb.DestroyEntity(entityInQueryIndex, entity);
            }).Schedule();
            
            _endSimulationEcbSystem.AddJobHandleForProducer(Dependency);
        }
    }
}
