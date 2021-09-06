using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.NetCode;
using Unity.Physics;
using Unity.Transforms;
using UnityEngine;
using Vermetio;

namespace Vermetio.Server
{
    [UpdateInGroup(typeof(GhostPredictionSystemGroup))]
    [UpdateInWorld(UpdateInWorld.TargetWorld.Server)] // no client side prediction for now
    [UpdateBefore(typeof(BulletSystem))]
    public class CannonAimingSystem : SystemBase
    {
        private GhostPredictionSystemGroup _ghostPredictionSystemGroup;
        private EndSimulationEntityCommandBufferSystem _endSimulationEcbSystem;

        protected override void OnCreate()
        {
            _ghostPredictionSystemGroup = World.GetExistingSystem<GhostPredictionSystemGroup>();
            _endSimulationEcbSystem = World.GetOrCreateSystem<EndSimulationEntityCommandBufferSystem>();
        }
        
        protected override void OnUpdate()
        {
            var tick = _ghostPredictionSystemGroup.PredictingTick;
            var deltaTime = Time.DeltaTime;
            var elapsedTime = Time.ElapsedTime;

            Entities
                // .WithoutBurst()
                .WithName("Boat_cage_rotation")
                .WithAll<BoatCageTag>()
                .ForEach((ref Rotation rotation, in Parent parent, in LocalToParent localToParent,
                    in LocalToWorld localToWorld) =>
                {
                    var inputBuffer = GetBuffer<BoatInput>(parent.Value);
                    inputBuffer.GetDataAtTick(tick, out var input);
                    
                    var angleToReticle = localToWorld.Up.SignedAngleDeg(math.normalize(Flatten(localToWorld.Forward)), math.normalize(Flatten(input.AimPosition - localToWorld.Position)));
                    Debug.DrawLine(localToWorld.Position, localToWorld.Position + Flatten(math.normalize(localToWorld.Forward)) * 10, Color.red, deltaTime);
                    Debug.DrawLine(localToWorld.Position, localToWorld.Position + Flatten(math.normalize(input.AimPosition - localToWorld.Position) * 10), Color.green, deltaTime);
                    if (math.abs(angleToReticle) < 2f) // close enough
                        return;
                        
                    rotation.Value = math.mul(quaternion.AxisAngle(new float3(0, 1, 0), math.radians(math.sign(angleToReticle) * 180f) * deltaTime), rotation.Value);
                    
                    
                }).Run();
            
            var bulletPrefab = EntityHelpers.GetGhostPrefab<BulletTag>(EntityManager);
            var gravityFactor = GetComponent<PhysicsGravityFactor>(bulletPrefab).Value;
            if (!TryGetSingleton<PhysicsStep>(out var physicsStep))
            {
                physicsStep = PhysicsStep.Default;
            }
            
            var endFrameEcb = _endSimulationEcbSystem.CreateCommandBuffer().AsParallelWriter();
            
            Dependency = Entities
                .WithName("Prepare_shoot_paramaters") // could be slow, 2 indirect lookups
                .WithAll<MovableBoatComponent>()
                .ForEach((Entity entity, int entityInQueryIndex, ref ShootParametersComponent shootParams, in BulletSpawnPointReference spawnPointReference, in LocalToWorld ltw) =>
                {
                    var inputBuffer = GetBuffer<BoatInput>(entity);
                    inputBuffer.GetDataAtTick(tick, out var input);

                    var spawnPoint = GetComponent<LocalToWorld>(spawnPointReference.BulletSpawnPoint).Position;
                    
                    var maxSpeed = 200f;
                    var toTarget = input.AimPosition - spawnPoint;
                    var gravity = physicsStep.Gravity * gravityFactor;
                    var gSquared = math.lengthsq(gravity);
                    var b = maxSpeed * maxSpeed + math.dot(toTarget, gravity);
                    var discriminant = b * b - gSquared * math.lengthsq(toTarget);

                    if (discriminant < 0 || math.length(toTarget) < 2 * math.distance(ltw.Position, spawnPoint))
                    {
                        // Target to far to hit with given max speed
                        shootParams.TargetLegit = false;
                        return;
                    }

                    float discRoot = math.sqrt(discriminant);
                    
                    // Highest shot with the given max speed:
                    float T_max = math.sqrt((b + discRoot) * 2f / gSquared);
                    
                    // Most direct shot with the given max speed:
                    float T_min = math.sqrt((b - discRoot) * 2f / gSquared);

                    // Lowest-speed arc available:
                    float T_lowEnergy = math.sqrt(math.sqrt( math.lengthsq(toTarget) * 4f/gSquared));

                    // Pick one of the above Ts, or something in-between
                    float T = math.lerp(T_min, T_max, 0.1f); // TODO: tweak

                    // Convert from time-to-hit to a launch velocity:
                    var velocity = toTarget / T - gravity * T / 2f;

                    var afterCooldown = elapsedTime - shootParams.LastShotAt > shootParams.Cooldown;
                    shootParams.TargetLegit = true;
                    shootParams.Velocity = velocity;
                }).Schedule(Dependency);

            Entities
                .WithName("Rotate_cannon_axle")
                .WithAll<MovableBoatComponent>()
                .ForEach((in CannonAxleReference axleReference, in ShootParametersComponent shootParams) =>
                {
                    if (!shootParams.TargetLegit)
                        return;
                    
                    var angle = new float3(1, 0, 0).SignedAngleDeg(new float3(0, 1, 0), math.normalize(shootParams.Velocity));
                    var axleRotation = quaternion.AxisAngle(new float3(1, 0, 0), math.radians(math.abs(angle))); // buggy when close to boat
                    SetComponent(axleReference.Axle, new Rotation() { Value = axleRotation });
                }).Schedule();

            // Dependency = JobHandle.CombineDependencies(Dependency, prepareJob, rotateAxleJob);

            _endSimulationEcbSystem.AddJobHandleForProducer(Dependency);
        }

        private static float3 Flatten(float3 vector) => new float3(vector.x, 0f, vector.z);
    }
}
