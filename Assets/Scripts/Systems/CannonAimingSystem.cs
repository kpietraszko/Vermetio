using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.NetCode;
using Unity.Transforms;
using Vermetio;

namespace Vermetio.Server
{
    [UpdateInGroup(typeof(GhostPredictionSystemGroup))]
    [UpdateInWorld(UpdateInWorld.TargetWorld.Server)] // no client side prediction for now
    public class CannonAimingSystem : SystemBase
    {
        private GhostPredictionSystemGroup _ghostPredictionSystemGroup;

        protected override void OnCreate()
        {
            _ghostPredictionSystemGroup = World.GetExistingSystem<GhostPredictionSystemGroup>();
        }
        
        protected override void OnUpdate()
        {
            // Assign values to local variables captured in your job here, so that it has
            // everything it needs to do its work when it runs later.
            // For example,
            //     float deltaTime = Time.DeltaTime;

            // This declares a new kind of job, which is a unit of work to do.
            // The job is declared as an Entities.ForEach with the target components as parameters,
            // meaning it will process all entities in the world that have both
            // Translation and Rotation components. Change it to process the component
            // types you want.
            var tick = _ghostPredictionSystemGroup.PredictingTick;
            var deltaTime = Time.DeltaTime;
            var mouseInputsPerEntity = GetBufferFromEntity<BoatMouseInput>(true);
            var parentPerEntity = GetComponentDataFromEntity<Parent>(true);

            Entities.WithoutBurst().WithAll<BoatCageTagComponent>()
                .ForEach((ref Rotation rotation, in Parent parent, in LocalToParent localToParent,
                    in LocalToWorld localToWorld) =>
                {
                    var parentsParent = parentPerEntity[parent.Value].Value;
                    var mouseInputBuffer = mouseInputsPerEntity[parentsParent];
                    mouseInputBuffer.GetDataAtTick(tick, out var mouseInput);
                    var angleToReticle = localToWorld.Up.SignedAngle(localToWorld.Forward, mouseInput.AimPosition);
                    rotation.Value = math.mul(quaternion.AxisAngle(new float3(0, 1, 0), angleToReticle),
                        rotation.Value);
                }).Run();
        }
    }
}
