using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.NetCode;
using Unity.Transforms;
using UnityEngine;
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

            Entities.WithoutBurst().WithAll<BoatCageTag>()
                .ForEach((ref Rotation rotation, in Parent parent, in LocalToParent localToParent,
                    in LocalToWorld localToWorld) =>
                {
                    var inputBuffer = inputsPerEntity[parent.Value];
                    inputBuffer.GetDataAtTick(tick, out var input);
                    
                    var angleToReticle = localToWorld.Up.SignedAngleDeg(math.normalize(Flatten(localToWorld.Forward)), math.normalize(Flatten(input.AimPosition - localToWorld.Position)));
                    Debug.DrawLine(localToWorld.Position, localToWorld.Position + Flatten(math.normalize(localToWorld.Forward)) * 10, Color.red, deltaTime);
                    Debug.DrawLine(localToWorld.Position, localToWorld.Position + Flatten(math.normalize(input.AimPosition - localToWorld.Position) * 10), Color.green, deltaTime);
                    if (math.abs(angleToReticle) < 2f) // close enough
                        return;
                        
                    rotation.Value = math.mul(quaternion.AxisAngle(new float3(0, 1, 0), math.radians(math.sign(angleToReticle) * 180f) * deltaTime), rotation.Value);
                }).Run();
        }

        private float3 Flatten(float3 vector) => new float3(vector.x, 0f, vector.z);
    }
}
