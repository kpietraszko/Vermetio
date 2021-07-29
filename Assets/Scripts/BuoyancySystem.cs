using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using Crest;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using static Unity.Mathematics.math;
using Unity.NetCode;
using Unity.Physics;
using Unity.Physics.Extensions;
using Unity.Physics.Systems;
using Unity.Profiling;
using Unity.Transforms;
using UnityEngine;
using UnityEngine.Profiling;
using BoxCollider = Unity.Physics.BoxCollider;
using Debug = UnityEngine.Debug;
using ForceMode = Unity.Physics.Extensions.ForceMode;
using Math = System.Math;
using quaternion = Unity.Mathematics.quaternion;
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

        private static Vector3[] _queryPoints;
        private static float[] _waterHeights;
        private static Vector3[] _normals;
        private static Vector3[] _velocities;

        private static bool _debugDraw = true;

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

            var voxelizationMarker = new ProfilerMarker(("Voxelization"));

            var ecb = _ghostSimulationSystemGroup.PostUpdateCommands.AsParallelWriter();
            
            // _buildPhysicsWorld.AddInputDependencyToComplete(Dependency);

            var elapsedTime = Time.ElapsedTime;

            var collProvider = OceanRenderer.Instance.CollisionProvider;

            var numberOfBuoyantObjects = GetEntityQuery(typeof(BuoyantComponent)).CalculateEntityCount();
            
            var entities = new NativeArray<Entity>(numberOfBuoyantObjects, Allocator.TempJob);
            
            #region AllocateStaticArrays
            if (_queryPoints?.Length != numberOfBuoyantObjects)
                _queryPoints = new Vector3[numberOfBuoyantObjects];

            if (_waterHeights?.Length != numberOfBuoyantObjects)
                _waterHeights = new float[numberOfBuoyantObjects];

            if (_normals?.Length != numberOfBuoyantObjects)
                _normals = new Vector3[numberOfBuoyantObjects];

            if (_velocities?.Length != numberOfBuoyantObjects)
                _velocities = new Vector3[numberOfBuoyantObjects];
            #endregion

            var entityIndex = 0;

            Entities
                .WithoutBurst()
                .ForEach((Entity entity, in Translation translation, in BuoyantComponent buoyantComponent) =>
            {
                entities[entityIndex] = entity;
                _queryPoints[entityIndex] = translation.Value;
                entityIndex++;
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
            // TODO: same for normals and velocities

            for (int i = 0; i < numberOfBuoyantObjects; i++)
            {
                waterHeightsPerEntity.TryAdd(entities[i], _waterHeights[i]);
            }

            Entities
                // .WithoutBurst()
                .WithName("Apply_bouyancy")
                // .WithReadOnly(physicsWorld)
                // .WithReadOnly(waterHeightsPerEntity)
                .ForEach((ref Translation translation, ref PhysicsVelocity pv, ref PhysicsDamping damping,
                    ref BuoyantComponent buoyant,
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
                    
                    // if (tick % 60 == 0)
                    //     Debug.Log($"{tick / 60}");
                    
                    if (_debugDraw)
                    {
                        Debug.DrawLine(translation.Value + 5f * float3(0,1f,0), translation.Value + 5f * float3(0,1f,0) + waterSurfaceVel,
                            new Color(1, 1, 1, 0.6f));
                    }

                    // var ecb = _ghostSimulationSystemGroup.PostUpdateCommands.AsParallelWriter();
                    var submergedFactor = lerp(buoyant.SubmergedPercentage, submergedAmount, 0.25f);
                    buoyant.SubmergedPercentage = submergedFactor;
                    var baseDampingLinear = 0.04f;
                    var baseDampingAngular = 1f; //1.5f;
                    // damping = new PhysicsDamping()
                    // {
                    //     Linear = baseDampingLinear + baseDampingLinear * (submergedFactor * 10f),
                    //     Angular = baseDampingAngular + baseDampingAngular * (submergedFactor * 0.5f)
                    // };
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