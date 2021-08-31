using Unity.Entities;
using Unity.Jobs;
using Unity.NetCode;
using Unity.Physics.Systems;

// [UpdateAfter(typeof(FixedStepSimulationSystemGroup))]
// [UpdateBefore(typeof(GhostSimulationSystemGroup))]
// public class PhysicsNetCodeOrderingSystem : JobComponentSystem
// {
//     protected override JobHandle OnUpdate(JobHandle inputDeps)
//     {
//         return inputDeps;
//     }
// }