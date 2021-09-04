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
using ForceMode = Unity.Physics.Extensions.ForceMode;

namespace Vermetio.Server
{
    [UpdateInGroup(typeof(GhostPredictionSystemGroup))]
    [UpdateInWorld(UpdateInWorld.TargetWorld.Server)] // no client side prediction for now
    public class BulletSystem : SystemBase
    {
        private EndSimulationEntityCommandBufferSystem _endSimulationEcbSystem;

        protected override void OnCreate()
        {
            base.OnCreate();
            _endSimulationEcbSystem = World.GetOrCreateSystem<EndSimulationEntityCommandBufferSystem>();
        }

        private struct BulletFiredComponent : ISystemStateComponentData
        {
        }


        protected override void OnUpdate()
        {
            var endFrameEcb = _endSimulationEcbSystem.CreateCommandBuffer().AsParallelWriter();
            var deltaTime = Time.DeltaTime;
            if (!TryGetSingleton<PhysicsStep>(out var physicsStep))
            {
                physicsStep = PhysicsStep.Default;
            }

            Entities
                .WithAll<BulletTag>().WithNone<BulletFiredComponent>()
                .ForEach((Entity entity, int entityInQueryIndex, ref LocalToWorld localToWorld, ref PhysicsVelocity pv,
                    in PhysicsMass pm, in ShootCommand cmd, in PhysicsGravityFactor gravityFactor, in SpawnedByComponent spawnedBy) =>
                {
                    endFrameEcb.AddComponent<BulletFiredComponent>(entityInQueryIndex, entity);

                    var maxSpeed = 200f;
                    var toTarget = cmd.TargetPosition - localToWorld.Position;
                    var gravity = physicsStep.Gravity * gravityFactor.Value;
                    var gSquared = math.lengthsq(gravity);
                    var b = maxSpeed * maxSpeed + math.dot(toTarget, gravity);
                    var discriminant = b * b - gSquared * math.lengthsq(toTarget);

                    if (discriminant < 0)
                    {
                        Debug.Log("Target to far to hit with given max speed");
                        endFrameEcb.DestroyEntity(entityInQueryIndex, entity);
                    }

                    float discRoot = math.sqrt(discriminant);
                    
                    // Highest shot with the given max speed:
                    float T_max = math.sqrt((b + discRoot) * 2f / gSquared);
                    
                    // Most direct shot with the given max speed:
                    float T_min = math.sqrt((b - discRoot) * 2f / gSquared);

                    // Lowest-speed arc available:
                    float T_lowEnergy = math.sqrt(math.sqrt( math.lengthsq(toTarget) * 4f/gSquared));

                    // Pick one of the above Ts, or something in-between
                    float T = math.lerp(T_min, T_max, 0.1f);

                    // Convert from time-to-hit to a launch velocity:
                    var velocity = toTarget / T - gravity * T / 2f;

                    pm.GetImpulseFromForce(velocity, ForceMode.VelocityChange, deltaTime, out var impulse, out var impulseMass);
                    pv.ApplyLinearImpulse(impulseMass, impulse);

                    var axleRotation = quaternion.LookRotation(math.cross(new float3(velocity), new float3(1, 0, 0)), math.normalize(velocity)); // TODO: very wrong
                    var axleReference = GetComponent<CannonAxleReference>(spawnedBy.Player).Axle; // these 2 get auto-converted to GetComponentDataFromEntity, indirect access, can be slow
                    SetComponent(axleReference, new Rotation() { Value = axleRotation });
                }).Schedule();
            
            
        }
    }
}