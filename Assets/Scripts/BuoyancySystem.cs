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
    [UpdateInWorld(UpdateInWorld.TargetWorld.Server)]
    public class BuoyancySystem : SystemBase
    {
        BuildPhysicsWorld _buildPhysicsWorld;
        ExportPhysicsWorld _exportPhysicsWorld;
        EndFramePhysicsSystem _endFramePhysics;
        GhostSimulationSystemGroup _ghostSimulationSystemGroup;
        private float _initialHeight;

        private static List<Vector3> _queryPoints = new List<Vector3>(512);
        private static List<float> _waterHeights = new List<float>(512);
        private static List<Vector3> _normals = new List<Vector3>(512);
        private static List<Vector3> _velocities = new List<Vector3>(512);

        private static bool _debugDraw = false;

        protected override void OnCreate()
        {
            base.OnCreate();
            // RequireSingletonForUpdate<WaveSpectrumComponent>();
            // RequireSingletonForUpdate<WavelengthElement>();
            // RequireSingletonForUpdate<WaveAmplitudeElement>();
            // RequireSingletonForUpdate<WaveAngleElement>();
            // RequireSingletonForUpdate<PhaseElement>();

            _buildPhysicsWorld = World.GetOrCreateSystem<BuildPhysicsWorld>();
            _exportPhysicsWorld = World.GetOrCreateSystem<ExportPhysicsWorld>();
            _endFramePhysics = World.GetOrCreateSystem<EndFramePhysicsSystem>();
            _ghostSimulationSystemGroup = World.GetExistingSystem<GhostSimulationSystemGroup>();

            // Debug.unityLogger.logEnabled = false;
        }

        protected override void OnUpdate()
        {
            var deltaTime = Time.DeltaTime;
            var tick = World.GetExistingSystem<ServerSimulationSystemGroup>().ServerTick;
            var physicsWorld = _buildPhysicsWorld.PhysicsWorld;

            Dependency = JobHandle.CombineDependencies(Dependency, _endFramePhysics.GetOutputDependency());

            // _buildPhysicsWorld.AddInputDependencyToComplete(Dependency);

            var collProvider = OceanRenderer.Instance.CollisionProvider;
            
            var numberOfBuoyantObjects = GetEntityQuery(typeof(BuoyantComponent)).CalculateEntityCount();

            var entities = new NativeArray<Entity>(numberOfBuoyantObjects, Allocator.TempJob);

            var entityIndex = 0;
            
            _queryPoints.Clear();
            _waterHeights.Clear();
            _normals.Clear();
            _velocities.Clear();

            Entities
                .WithoutBurst()
                .ForEach((Entity entity, in Translation translation, in BuoyantComponent buoyantComponent) =>
            {
                entities[entityIndex++] = entity;
                _queryPoints.Add(translation.Value);
            }).Run();

            // for (int i = 0; i < queryPoints.Length; i++)
            // {
            //     queryPoints[i] = new Vector3(allVoxels[i].x, 0f, allVoxels[i].y);
            // }

            if (!collProvider.RetrieveSucceeded(collProvider.Query(GetHashCode(), 0f, _queryPoints, _waterHeights, _normals, _velocities)))
            {
                Debug.LogError("Height query failed");
            }

            var waterHeightsPerEntity = new NativeHashMap<Entity, float>(numberOfBuoyantObjects, Allocator.TempJob);
            var waterNormalsPerEntity = new NativeHashMap<Entity, float3>(numberOfBuoyantObjects, Allocator.TempJob);
            var waterVelocitiesPerEntity = new NativeHashMap<Entity, float3>(numberOfBuoyantObjects, Allocator.TempJob);

            for (int i = 0; i < numberOfBuoyantObjects; i++)
            {
                waterHeightsPerEntity.TryAdd(entities[i], _waterHeights[i]);
                waterNormalsPerEntity.TryAdd(entities[i], _normals[i]);
                waterVelocitiesPerEntity.TryAdd(entities[i], _velocities[i]);
            }

            var debugDraw = _debugDraw;

            Entities
                // .WithoutBurst()
                .WithName("Apply_bouyancy")
                // .WithReadOnly(physicsWorld)
                // .WithReadOnly(waterHeightsPerEntity)
                .ForEach((Entity entity, ref Translation translation, ref PhysicsVelocity pv,
                    ref BuoyantComponent buoyant, in LocalToWorld localToWorld, 
                    in Rotation rotation, in PhysicsMass pm, in PhysicsCollider col) =>
                {
                    // ProfileFewTicks(tick);
                    // GizmoManager.ClearGizmos();
                    var objectExtents = col.Value.Value
                        .CalculateAabb(new RigidTransform(rotation.Value, translation.Value))
                        .Extents;

                    // var objectSizeForWaves = min(objectExtents.x, min(objectExtents.y, objectExtents.z));
                    // Debug.Log($"{voxels.Length}");
                    // Debug.Log($"{tick}");

                    var submergedAmount = 0f;
                    var waterVelocity = waterVelocitiesPerEntity[entity];
                    var velocityRelativeToWater = pv.Linear - waterVelocity;
                    
                    if (debugDraw)
                    {
                        Debug.DrawLine(translation.Value + 5f * float3(0,1f,0), translation.Value + 5f * float3(0,1f,0) + waterVelocity,
                            new Color(1, 1, 1, 0.6f));
                    }
                    
                    var height = waterHeightsPerEntity[entity];
                    var normal = waterNormalsPerEntity[entity];
                    var raiseObject = 1f; // TODO: PARAMETER
                    var buoyancyCoeff = 3f; // TODO: PARAMETER
                    var accelerateDownhill = 0f; // TODO: PARAMETER
                    var forceHeightOffset = -0.3f; // TODO: PARAMETER
                    var dragInWaterUp = 3f;  // TODO: PARAMETER
                    var dragInWaterRight = 2f; // TODO: PARAMETER
                    var dragInWaterForward = 1f; // TODO: PARAMETER
                    var bouyancyTorque = 8f; // TODO: PARAMETER
                    var dragInWaterRotational = 0.2f; // TODO: PARAMETER
                    var bottomDepth = height - translation.Value.y + raiseObject;
                    var inWater = bottomDepth > 0f;
                    if (!inWater)
                        return;
                    
                    var up = new float3(0f, 1f, 0f);
                    var buoyancy = up * buoyancyCoeff * bottomDepth * bottomDepth * bottomDepth;
                    // Debug.Log($"pmTransformPos: {pm.Transform.pos}");
                    pm.GetImpulseFromForce(buoyancy, ForceMode.Acceleration, deltaTime, out var impulse, out var impulseMass);
                    pv.ApplyLinearImpulse(pm, impulse); // this is fucking 10 times too strong for some reason

                    // // Approximate hydrodynamics of sliding along water
                    // if (accelerateDownhill > 0f)
                    // {
                    //     pm.GetImpulseFromForce(new float3(normal.x, 0f, normal.z) * -PhysicsStep.Default.Gravity.y * accelerateDownhill, ForceMode.Acceleration, deltaTime, out impulse, out impulseMass);
                    //     pv.ApplyImpulse(impulseMass, translation, rotation, impulse, translation.Value);
                    // }
                    
                    // Apply drag relative to water
                    var forcePosition = translation.Value + forceHeightOffset * up;
                    pm.GetImpulseFromForce(up * dot(up, -velocityRelativeToWater) * dragInWaterUp, ForceMode.Acceleration, deltaTime, out impulse, out impulseMass);
                    pv.ApplyImpulse(impulseMass, translation, rotation, impulse, forcePosition);
                    // skipping right and forward drag because only vertical velocities are baked
                    
                    
                    // Align to normal
                    if (debugDraw) 
                        Debug.DrawLine(translation.Value, translation.Value + 5f * normal, Color.green);
                    
                    var torqueWidth = Vector3.Cross(localToWorld.Up, normal);
                    pv.ApplyAngularImpulse(impulseMass, torqueWidth * bouyancyTorque * deltaTime); // TODO: maybe * deltaTime?
                    pv.ApplyAngularImpulse(pm, -dragInWaterRotational * pv.Angular * deltaTime); // TODO: maybe * deltaTime?

                    // if (tick % 60 == 0)
                    //     Debug.Log($"{tick / 60}");
                    
                    if (debugDraw)
                    {
                        Debug.DrawLine(translation.Value + 5f * float3(0,1f,0), translation.Value + 5f * float3(0,1f,0) + waterVelocity,
                            new Color(1, 1, 1, 0.6f));
                    }
                }).Schedule();

            _buildPhysicsWorld.AddInputDependencyToComplete(Dependency);
        }

#if UNITY_EDITOR
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
    }
}