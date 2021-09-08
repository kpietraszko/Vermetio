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
            
            var rttPerEntity = new NativeHashMap<Entity, float>(100, Allocator.TempJob);

            Entities
                .ForEach((NetworkSnapshotAckComponent ack, CommandTargetComponent target) =>
                {
                    rttPerEntity.Add(target.targetEntity, ack.EstimatedRTT / 1000); // ack.EstimatedRTT is in ms
                }).Run();

            Entities
                // .WithoutBurst()
                .WithName("Boat_cage_rotation")
                .WithReadOnly(rttPerEntity)
                .WithDisposeOnCompletion(rttPerEntity)
                .ForEach((Entity playerEntity, 
                    in PredictedGhostComponent prediction,
                    in BoatCageReference cageReference,
                    in DynamicBuffer<BoatInput> inputBuffer,
                    in ShootParametersComponent shootParams,
                    in LocalToWorld localToWorld) =>
                {
                    if (!GhostPredictionSystemGroup.ShouldPredict(tick, prediction))
                        return;
                    
                    var rtt = rttPerEntity[playerEntity];
                    var snapshotTravelTime = rtt / 2d;
                    var afterCooldown = elapsedTime + snapshotTravelTime - shootParams.LastShotRequestedAt > shootParams.MinimumShotDelay;
                    if (!afterCooldown)
                        return;
                    
                    inputBuffer.GetDataAtTick(tick, out var input);

                    if (!math.any(input.AimPosition))
                        return;

                    var cageLtw = GetComponent<LocalToWorld>(cageReference.Cage);
                    var angleToReticle = cageLtw.Up.SignedAngleDeg(math.normalize(Flatten(localToWorld.Forward)), math.normalize(Flatten(input.AimPosition - cageLtw.Position)));
                    Debug.DrawLine(cageLtw.Position, cageLtw.Position + Flatten(math.normalize(cageLtw.Forward)) * 10, Color.red, deltaTime);
                    Debug.DrawLine(cageLtw.Position, cageLtw.Position + Flatten(math.normalize(input.AimPosition - cageLtw.Position) * 10), Color.green, deltaTime);
                    // if (math.abs(angleToReticle) < 3f) // close enough
                    //     return;
                    
                    SetComponent(cageReference.Cage, new Rotation()
                    {
                        // Value = math.mul(rotation.Value, 
                        //         quaternion.AxisAngle(new float3(0, 1, 0),
                        //             math.sign(angleToReticle) * (deltaTime / shootParams.MinimumShotDelay) *
                        //             math.radians(180f)))
                        Value = quaternion.AxisAngle(new float3(0, 1, 0), math.radians(angleToReticle))

                    });
                }).Run();
            
            var bulletPrefab = EntityHelpers.GetGhostPrefab<BulletTag>(EntityManager);
            var gravityFactor = GetComponent<PhysicsGravityFactor>(bulletPrefab).Value;
            if (!TryGetSingleton<PhysicsStep>(out var physicsStep))
            {
                physicsStep = PhysicsStep.Default;
            }
            
            var endFrameEcb = _endSimulationEcbSystem.CreateCommandBuffer().AsParallelWriter();
            
            Dependency = Entities
                .WithName("Prepare_shot_velocity") // could be slow, 2 indirect lookups
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
                    
                    var afterCooldown = elapsedTime - shootParams.LastShotRequestedAt > shootParams.MinimumShotDelay;
                    if (!afterCooldown)
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
