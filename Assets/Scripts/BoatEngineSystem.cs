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
            // Debug.Log($"Applying impulse {impulse}");

            var rotationAxis = math.normalizesafe(localToWorld.Up + probyBuoyant.TurningHeel * localToWorld.Forward);
            // var rotationToTarget = Math.FromToRotation(localToWorld.Forward, targetHeading.Value);
            var angleToTargetInRad = SignedAngle(localToWorld.Forward, input.TargetDirection, new float3(0,1,0));
            Debug.Log($"{angleToTargetInRad}");
            Debug.DrawLine(translation.Value, translation.Value + localToWorld.Forward * 10f, Color.red, deltaTime);
            Debug.DrawLine(translation.Value, translation.Value + input.TargetDirection * 10f, Color.yellow, deltaTime);

            pv.ApplyAngularImpulse(impulseMass, rotationAxis * math.clamp(angleToTargetInRad, -10f, 10f) * probyBuoyant.TurnPower * deltaTime);
        }).Schedule();
    }

    // Returns the angle in degrees between /from/ and /to/. This is always the smallest
    private static float Angle(float3 from, float3 to)
    {
        const float unityEpsilonNormalSqrt = 1e-15F;
        // sqrt(a) * sqrt(b) = sqrt(a * b) -- valid for real numbers
        var denominator = math.sqrt(math.lengthsq(from) * math.lengthsq(to));
        if (denominator < unityEpsilonNormalSqrt)
            return 0F;

        var dot = math.clamp(math.dot(from, to) / denominator, -1F, 1F);
        return math.degrees(math.acos(dot));
    }

    // The smaller of the two possible angles between the two vectors is returned, therefore the result will never be greater than 180 degrees or smaller than -180 degrees.
    // If you imagine the from and to vectors as lines on a piece of paper, both originating from the same point, then the /axis/ vector would point up out of the paper.
    // The measured angle between the two vectors would be positive in a clockwise direction and negative in an anti-clockwise direction.
    private static float SignedAngle(float3 from, float3 to, float3 axis)
    {
        var unsignedAngle = Angle(from, to);
        var sign = math.sign(math.dot(math.cross(from, to), axis));
        return unsignedAngle * sign;
    }
}