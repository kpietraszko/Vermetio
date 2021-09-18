using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.NetCode;
using Unity.Transforms;

namespace Vermetio.Server
{
    [UpdateInGroup(typeof(ServerSimulationSystemGroup))]
    [UpdateInWorld(UpdateInWorld.TargetWorld.Server)]
    [UpdateAfter(typeof(BoatHitSystem))]
    public class DieSystem : SystemBase
    {
        private EndSimulationEntityCommandBufferSystem _endSimulationEcbSystem;

        protected override void OnCreate()
        {
            base.OnCreate();
            _endSimulationEcbSystem = World.GetOrCreateSystem<EndSimulationEntityCommandBufferSystem>();
        }

        protected override void OnUpdate()
        {
            var endFrameEcb = _endSimulationEcbSystem.CreateCommandBuffer();

            Entities.ForEach((Entity entity, in HealthComponent health) =>
                {
                    if (health.Value <= 0)
                        endFrameEcb.DestroyEntity(entity);
                }).Schedule();

            _endSimulationEcbSystem.AddJobHandleForProducer(Dependency);
        }
    }
}
