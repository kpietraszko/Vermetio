using System.Collections.Generic;
using System.Diagnostics;
using Crest;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.NetCode;
using Unity.Physics;
using Unity.Physics.Extensions;
using Unity.Physics.Systems;
using Unity.Profiling;
using Unity.Transforms;
using UnityEngine;
using UnityEngine.Profiling;
using static Unity.Mathematics.math;
using Debug = UnityEngine.Debug;
using ForceMode = Unity.Physics.Extensions.ForceMode;
#if UNITY_EDITOR
using UnityEditorInternal;

#endif

namespace Vermetio.Server
{
    [UpdateInGroup(typeof(GhostSimulationSystemGroup))]
    [UpdateInWorld(UpdateInWorld.TargetWorld.Server)] // probably redundant
    public class SimpleBuoyancySystem : SystemBase
    {
        private BuildPhysicsWorld _buildPhysicsWorld;
        private EndFramePhysicsSystem _endFramePhysics;
        private EndSimulationEntityCommandBufferSystem _endSimulationEcbSystem;

        private static Vector3[] _queryPoints = new Vector3[0];
        private static float[] _waterHeights = new float[0];
        private static Vector3[] _normals = new Vector3[0];
        private static Vector3[] _velocities = new Vector3[0];
        private EntityQuery _simpleBuoyantQuery;
        private static bool _debugDraw = false;

        protected override void OnCreate()
        {
            base.OnCreate();

            _buildPhysicsWorld = World.GetOrCreateSystem<BuildPhysicsWorld>();
            _endFramePhysics = World.GetOrCreateSystem<EndFramePhysicsSystem>();

            _endSimulationEcbSystem = World.GetOrCreateSystem<EndSimulationEntityCommandBufferSystem>();
            // Debug.unityLogger.logEnabled = false;
        }

        protected override void OnUpdate()
        {
            var deltaTime = Time.DeltaTime;
            var tick = World.GetExistingSystem<ServerSimulationSystemGroup>().ServerTick;

            Dependency = JobHandle.CombineDependencies(Dependency, _endFramePhysics.GetOutputDependency());

            // _buildPhysicsWorld.AddInputDependencyToComplete(Dependency);

            var collProvider = OceanRenderer.Instance.CollisionProvider;
            
            var numberOfBuoyantObjects = GetEntityQuery(typeof(SimpleBuoyantComponent)).CalculateEntityCount();

            var entities = new NativeArray<Entity>(numberOfBuoyantObjects, Allocator.TempJob);

            if (_queryPoints?.Length != numberOfBuoyantObjects)
            {
                Debug.Log("Simple array size mismatch - reallocating");
                _queryPoints = new Vector3[numberOfBuoyantObjects];
                _waterHeights = new float[numberOfBuoyantObjects];
                _normals = new Vector3[numberOfBuoyantObjects];
                _velocities = new Vector3[numberOfBuoyantObjects];
            }

            Entities
                .WithoutBurst()
                // .WithStoreEntityQueryInField(ref _simpleBuoyantQuery)
                .ForEach((Entity entity, int entityInQueryIndex, in Translation translation, in SimpleBuoyantComponent buoyantComponent) =>
            {
                entities[entityInQueryIndex] = entity;
                _queryPoints[entityInQueryIndex] = translation.Value;
            }).Run();

            var status = collProvider.Query(GetHashCode(), 0f, _queryPoints, _waterHeights, _normals, _velocities);
            if (!collProvider.RetrieveSucceeded(status))
            {
                #if UNITY_EDITOR
                Debug.LogWarning($"Simple query failed: {(CollProviderBakedFFT.QueryStatus)status}");
                Debug.Log($"Fail at {tick}");
                #endif
                entities.Dispose();
                return;
            }
            
            var waterDataPerEntity = new NativeHashMap<Entity, EntityWaterData>(numberOfBuoyantObjects, Allocator.TempJob);

            for (int i = 0; i < numberOfBuoyantObjects; i++)
            {
                waterDataPerEntity.TryAdd(entities[i], new EntityWaterData()
                {
                    Height = _waterHeights[i],
                    Normal = _normals[i],
                    Velocity = _velocities[i]
                });
            };

            entities.Dispose();

            var debugDraw = _debugDraw;
            var endFrameEcb = _endSimulationEcbSystem.CreateCommandBuffer().AsParallelWriter();

            Entities
                // .WithoutBurst()
                .WithName("Apply_simple_buoyancy")
                // .WithReadOnly(physicsWorld)
                .WithReadOnly(waterDataPerEntity)
                .WithDisposeOnCompletion(waterDataPerEntity)
                .ForEach((Entity entity, int entityInQueryIndex, ref Translation translation, ref PhysicsVelocity pv,
                    ref SimpleBuoyantComponent buoyant, ref PhysicsDamping damping, in LocalToWorld localToWorld, in PhysicsMass pm) =>
                {
                    // ProfileFewTicks(tick);
                    // GizmoManager.ClearGizmos();
                    // var objectExtents = col.Value.Value
                    //     .CalculateAabb(new RigidTransform(rotation.Value, translation.Value))
                    //     .Extents;

                    // var objectSizeForWaves = min(objectExtents.x, min(objectExtents.y, objectExtents.z));
                    // Debug.Log($"{voxels.Length}");
                    // Debug.Log($"{tick}");
                    
                    var waterData = waterDataPerEntity[entity];
                    var velocityRelativeToWater = pv.Linear - waterData.Velocity;
                    
                    if (debugDraw)
                    {
                        Debug.DrawLine(translation.Value + 5f * float3(0,1f,0), translation.Value + 5f * float3(0,1f,0) + waterData.Velocity,
                            new Color(1, 1, 1, 0.6f));
                    }
                    
                    var bottomDepth = waterData.Height - translation.Value.y + buoyant.RaiseObject;
                    var inWater = bottomDepth > 0f;
                    if (!inWater)
                    {
                        damping = new PhysicsDamping() {Angular = damping.Angular, Linear = 0f};
                        return;
                    }

                    if (HasComponent<BulletTag>(entity)) // bullet is no longer a bullet once in water
                        endFrameEcb.RemoveComponent<BulletTag>(entityInQueryIndex, entity);

                    damping = new PhysicsDamping() {Angular = damping.Angular, Linear = 1.5f};
                    var up = new float3(0f, 1f, 0f);
                    var buoyancy = up * buoyant.BuoyancyCoeff * bottomDepth * bottomDepth * bottomDepth;
                    // Debug.Log($"pmTransformPos: {pm.Transform.pos}");
                    pm.GetImpulseFromForce(buoyancy, ForceMode.Acceleration, deltaTime, out var impulse, out var impulseMass);
                    pv.ApplyLinearImpulse(pm, impulse);
                    var rotation = new Rotation() {Value = localToWorld.Rotation.value};

                    // Approximate hydrodynamics of sliding along water
                    if (buoyant.AccelerateDownhill > 0f)
                    {
                        pm.GetImpulseFromForce(new float3(waterData.Normal.x, 0f, waterData.Normal.z) * -PhysicsStep.Default.Gravity.y * buoyant.AccelerateDownhill, ForceMode.Acceleration, deltaTime, out impulse, out impulseMass);
                        pv.ApplyImpulse(impulseMass, translation, rotation, impulse, translation.Value);
                    }
                    
                    // Apply drag relative to water
                    var forcePosition = translation.Value + buoyant.ForceHeightOffset * up;
                    pm.GetImpulseFromForce(up * dot(up, -velocityRelativeToWater) * buoyant.DragInWaterUp, ForceMode.Acceleration, deltaTime, out impulse, out impulseMass);
                    pv.ApplyImpulse(impulseMass, translation, rotation, impulse, forcePosition);
                    // skipping right and forward drag because only vertical velocities are baked

                    // Align to normal
                    if (debugDraw) 
                        Debug.DrawLine(translation.Value, translation.Value + 5f * waterData.Normal, Color.green);
                    
                    var torqueWidth = Vector3.Cross(localToWorld.Up, waterData.Normal);
                    pv.ApplyAngularImpulse(impulseMass, torqueWidth * buoyant.BouyancyTorque * deltaTime);
                    pv.ApplyAngularImpulse(pm, -buoyant.DragInWaterRotational * pv.Angular * deltaTime);

                    // if (tick % 60 == 0)
                    //     Debug.Log($"{tick / 60}");
                    
                    if (debugDraw)
                    {
                        Debug.DrawLine(translation.Value + 5f * float3(0,1f,0), translation.Value + 5f * float3(0,1f,0) + waterData.Velocity,
                            new Color(1, 1, 1, 0.6f));
                    }
                }).ScheduleParallel();

            _buildPhysicsWorld.AddInputDependencyToComplete(Dependency);
        }

#if UNITY_EDITOR
        /// <summary>
        /// Used for deep profiling when Profiler is unresponsive and you can't stop recording
        /// </summary>
        /// <param name="tick"></param>
        [Conditional("UNITY_EDITOR")]
        private static void ProfileFewTicks(uint tick)
        {
            if (tick == 20)
            {
                Profiler.enabled = true;
                ProfilerDriver.enabled = true;
                Debug.Log("Starting profiler");
            }

            if (tick == 25)
            {
                Profiler.enabled = false;
                ProfilerDriver.enabled = false;
                Debug.Log("Finishing profiler");
                Debug.Break();
            }
        }

#endif

        void DrawBounds(Bounds b, float delay = 0)
        {
            // bottom
            var p1 = new Vector3(b.min.x, b.min.y, b.min.z);
            var p2 = new Vector3(b.max.x, b.min.y, b.min.z);
            var p3 = new Vector3(b.max.x, b.min.y, b.max.z);
            var p4 = new Vector3(b.min.x, b.min.y, b.max.z);

            Debug.DrawLine(p1, p2, Color.blue, delay);
            Debug.DrawLine(p2, p3, Color.red, delay);
            Debug.DrawLine(p3, p4, Color.yellow, delay);
            Debug.DrawLine(p4, p1, Color.magenta, delay);

            // top
            var p5 = new Vector3(b.min.x, b.max.y, b.min.z);
            var p6 = new Vector3(b.max.x, b.max.y, b.min.z);
            var p7 = new Vector3(b.max.x, b.max.y, b.max.z);
            var p8 = new Vector3(b.min.x, b.max.y, b.max.z);

            Debug.DrawLine(p5, p6, Color.blue, delay);
            Debug.DrawLine(p6, p7, Color.red, delay);
            Debug.DrawLine(p7, p8, Color.yellow, delay);
            Debug.DrawLine(p8, p5, Color.magenta, delay);

            // sides
            Debug.DrawLine(p1, p5, Color.white, delay);
            Debug.DrawLine(p2, p6, Color.gray, delay);
            Debug.DrawLine(p3, p7, Color.green, delay);
            Debug.DrawLine(p4, p8, Color.cyan, delay);
        }
        
        private struct EntityWaterData
        {
            internal float Height;
            internal float3 Normal;
            internal float3 Velocity;
        }
    }
}