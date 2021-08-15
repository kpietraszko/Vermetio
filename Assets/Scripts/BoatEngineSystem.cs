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

[UpdateInGroup(typeof(GhostPredictionSystemGroup))]
[UpdateInWorld(UpdateInWorld.TargetWorld
    .Server)] // no client side prediction, it would probably be impossible currently with physics
public class BoatEngineSystem : SystemBase
{
    GhostPredictionSystemGroup m_GhostPredictionSystemGroup;

    protected override void OnCreate()
    {
        m_GhostPredictionSystemGroup = World.GetExistingSystem<GhostPredictionSystemGroup>();
    }

    protected override void OnUpdate()
    {
        var tick = m_GhostPredictionSystemGroup.PredictingTick;
        var deltaTime = Time.DeltaTime;

        Entities.ForEach((DynamicBuffer<BoatKeyboardInput> keyboardInputBuffer, ref Translation translation,
            ref Rotation rotation,
            ref PhysicsMass pm, ref PhysicsVelocity pv, in LocalToWorld localToWorld,
            in ProbyBuoyantComponent probyBuoyant) =>
        {
            keyboardInputBuffer.GetDataAtTick(tick, out var input);

            pm.GetImpulseFromForce(localToWorld.Forward * probyBuoyant.EnginePower * input.Throttle,
                ForceMode.Acceleration, deltaTime, out var impulse, out var impulseMass);
            Debug.Log($"{pm.Transform.pos}");
            var forcePoint = math.transform(localToWorld.Value, pm.Transform.pos);
            forcePoint.DrawCross(1f, Color.black, deltaTime);
            pv.ApplyImpulse(impulseMass, translation, rotation, impulse, translation.Value); // is point correct?
        }).Schedule();
    }
}