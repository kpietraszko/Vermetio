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
            var tick = _ghostPredictionSystemGroup.PredictingTick;
            var deltaTime = Time.DeltaTime;
            var inputsPerEntity = GetBufferFromEntity<BoatInput>(true);

            Entities.WithoutBurst().WithAll<BoatCageTagComponent>()
                .ForEach((ref Rotation rotation, in Parent parent, in LocalToParent localToParent,
                    in LocalToWorld localToWorld) =>
                {
                    var inputBuffer = inputsPerEntity[parent.Value];
                    inputBuffer.GetDataAtTick(tick, out var input);
                    var angleToReticle = localToWorld.Up.SignedAngle(localToWorld.Forward, input.AimPosition);
                    rotation.Value = math.mul(quaternion.AxisAngle(new float3(0, 1, 0), angleToReticle),
                        rotation.Value);
                }).Run();
        }
    }
}
