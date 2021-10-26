using System.IO;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.NetCode;
using Unity.Physics;
using Unity.Physics.Extensions;
using Unity.Transforms;
using UnityEngine;
using Vermetio;
using ForceMode = Unity.Physics.Extensions.ForceMode;

namespace Vermetio.Server
{
    [UpdateInGroup(typeof(GhostPredictionSystemGroup))]
    [UpdateInWorld(UpdateInWorld.TargetWorld.Server)] // no client side prediction, it would probably be impossible currently with physics
    public class BoatEngineSystem : SystemBase
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

            Entities
                .WithNone<BoatFullyAboveWaterTag>()
                // .WithoutBurst()
                .ForEach((DynamicBuffer<BoatInput> keyboardInputBuffer, ref Translation translation,
                    ref Rotation rotation,
                    ref PhysicsMass pm, ref PhysicsVelocity pv, in LocalToWorld localToWorld,
                    in ProbyBuoyantComponent probyBuoyant) =>
                {
                    keyboardInputBuffer.GetDataAtTick(tick, out var keyboardInput);

                    // Debug.DrawLine(translation.Value, math.transform(localToWorld.Value, pv.Linear), Color.green, deltaTime);
                    // Debug.DrawLine(translation.Value, math.transform(localToWorld.Value, pv.Angular), Color.red, deltaTime);

                    if (math.abs(keyboardInput.Throttle) < 0.001f) // don't add force OR ROTATION if no throttle
                        return;
                    
                    // using (StreamWriter sw = new StreamWriter("boatPosition.csv", true))
                    // {
                    //     sw.WriteLine($"{localToWorld.Position.x};{localToWorld.Position.z}");
                    // }
                    
                    localToWorld.Position.DrawCross(11f, Color.green, 1/60f);

                    // if (tick % 60 == 0)
                    //     Debug.Log($"{math.length(pv.Linear)}");

                    var force = localToWorld.Forward * probyBuoyant.EnginePower * keyboardInput.Throttle;
                    // Debug.Log($"{keyboardInput.Throttle}");
                    pm.GetImpulseFromForce(force, ForceMode.Acceleration, deltaTime, out var impulse,
                        out var impulseMass);
                    // Debug.Log($"{pm.Transform.pos}");
                    var forcePoint = math.transform(localToWorld.Value, pm.Transform.pos);
                    forcePoint.DrawCross(1f, Color.black, deltaTime);
                    pv.ApplyLinearImpulse(impulseMass, impulse);
                    // Debug.Log($"Applying impulse {impulse}");

                    // TODO: note that this axis is a hack because idk
                    var rotationAxis = new float3(0, 0, 1) + probyBuoyant.TurningHeel * new float3(0, 0, 1); //localToWorld.Up + probyBuoyant.TurningHeel * localToWorld.Forward; // localToWorld.Up or world up?
                    var angleToTarget = localToWorld.Up.SignedAngleDeg(
                        math.normalize(new float3(localToWorld.Forward.x, 0f, localToWorld.Forward.z)),
                        keyboardInput.TargetDirection);

                    if (math.abs(angleToTarget) < 3f) // close enough already
                        return;

                    pv.ApplyAngularImpulse(pm,
                        rotationAxis * math.sign(angleToTarget) * probyBuoyant.TurnPower * deltaTime);
                }).Run();
        }
    }
}