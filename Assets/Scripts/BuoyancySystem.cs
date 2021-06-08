using System;
using System.IO;
using System.Runtime.CompilerServices;
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
using ForceMode = Unity.Physics.Extensions.ForceMode;
using Math = System.Math;
using quaternion = Unity.Mathematics.quaternion;

[UpdateInGroup(typeof(GhostSimulationSystemGroup))]
[UpdateInWorld(UpdateInWorld.TargetWorld.Server)]
public class BuoyancySystem : SystemBase
{
    BuildPhysicsWorld _buildPhysicsWorld;
    ExportPhysicsWorld _exportPhysicsWorld;
    EndFramePhysicsSystem _endFramePhysics;
    GhostSimulationSystemGroup _ghostSimulationSystemGroup;
    private float _initialHeight;

    protected override void OnCreate()
    {
        base.OnCreate();
        RequireSingletonForUpdate<WaveSpectrumComponent>();
        RequireSingletonForUpdate<WavelengthElement>();
        RequireSingletonForUpdate<WaveAmplitudeElement>();
        RequireSingletonForUpdate<WaveAngleElement>();
        RequireSingletonForUpdate<PhaseElement>();

        _buildPhysicsWorld = World.GetOrCreateSystem<BuildPhysicsWorld>();
        _exportPhysicsWorld = World.GetOrCreateSystem<ExportPhysicsWorld>();
        _endFramePhysics = World.GetOrCreateSystem<EndFramePhysicsSystem>();
        _ghostSimulationSystemGroup = World.GetExistingSystem<GhostSimulationSystemGroup>();
    }

    protected override void OnUpdate()
    {
        // Assign values to local variables captured in your job here, so that it has
        // everything it needs to do its work when it runs later.
        // For example,
        //     float deltaTime = Time.DeltaTime;

        // This declares a new kind of job, which is a unit of work to do.
        // The job is declared as an Entities.ForEach with the target components as parameters,
        // meaning it will process all entities in the world that have both
        // Translation and Rotation components. Change it to process the component
        // types you want.

        var spectrum = GetSingleton<WaveSpectrumComponent>();
        var spectrumEntity = GetSingletonEntity<WaveSpectrumComponent>();
        var wavelengthBuffer = EntityManager.GetBuffer<WavelengthElement>(spectrumEntity);
        var waveAmplitudeBuffer = EntityManager.GetBuffer<WaveAmplitudeElement>(spectrumEntity);
        var waveAngleBuffer = EntityManager.GetBuffer<WaveAngleElement>(spectrumEntity);
        var phaseBuffer = EntityManager.GetBuffer<PhaseElement>(spectrumEntity);

        var elapsedTime = Time.ElapsedTime;
        var deltaTime = Time.DeltaTime;
        var tick = World.GetExistingSystem<ServerSimulationSystemGroup>().ServerTick;

        // string docPath =
        //     Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        //
        // using (StreamWriter outputFile = new StreamWriter(Path.Combine(docPath, "BuoyancyElapsedTime.csv"), append: true))
        // {
        //     outputFile.WriteLine($"{DateTime.Now.Ticks};{elapsedTime}");
        // }

        var physicsWorld = _buildPhysicsWorld.PhysicsWorld;

        Dependency = JobHandle.CombineDependencies(Dependency, _endFramePhysics.GetOutputDependency());

        var voxelizationMarker = new ProfilerMarker(("Voxelization"));

        var ecb = _ghostSimulationSystemGroup.PostUpdateCommands.AsParallelWriter();

        Entities
            .WithName("Voxelization")
            .WithoutBurst()
            .WithNone<VoxelElement>()
            .WithReadOnly(physicsWorld)
            .ForEach((Entity entity, int entityInQueryIndex, ref Translation translation, ref PhysicsVelocity pv,
                ref PhysicsMass pm, in Rotation rotation, in PhysicsCollider col,
                in BuoyantComponent buoyant) =>
            {
                // GizmoManager.ClearGizmos();
                var boundingBox = col.Value.Value.CalculateAabb();
                var rigidTransform = new RigidTransform(quaternion.identity, translation.Value);
                var worldSpaceBb = Unity.Physics.Math.TransformAabb(rigidTransform, boundingBox);
                var voxelResolution = min(0.51f,
                    min(worldSpaceBb.Extents.x,
                        min(worldSpaceBb.Extents.y,
                            worldSpaceBb.Extents
                                .z))); // represents the half size? of a voxel when creating the voxel representation (in world space)
                ecb.SetComponent(entityInQueryIndex, entity,
                    new BuoyantComponent() {VoxelResolution = voxelResolution});

                // DrawBounds(new Bounds(boundingBox.Center, boundingBox.Extents), deltaTime);
                var bufferCreated = false;
                DynamicBuffer<VoxelElement> voxelsBuffer = default;
                // Debug.Log($"{worldSpaceBb.Min}");
                for (var ix = boundingBox.Min.x; ix < boundingBox.Max.x; ix += voxelResolution)
                {
                    for (var iz = boundingBox.Min.z; iz < boundingBox.Max.z; iz += voxelResolution)
                    {
                        voxelizationMarker.Begin();
                        for (var iy = boundingBox.Min.y; iy < boundingBox.Max.y; iy += voxelResolution)
                        {
                            var x = (voxelResolution * 0.5f) + ix;
                            var y = (voxelResolution * 0.5f) + iy;
                            var z = (voxelResolution * 0.5f) + iz;

                            var p = new float3(x, y, z); // + worldSpaceBb.Center;
                            var worldSpaceP = transform(new RigidTransform(rotation.Value, translation.Value), p);

                            // TODO: check if p is inside collider

                            // GizmoManager.AddGizmoAction(() =>
                            // {
                            //     Gizmos.color = new Color(0, 1, 0, 1f);
                            //     Gizmos.DrawWireCube(worldSpaceP, new float3(1.1f));
                            // });

                            var pointDistanceInput = new PointDistanceInput()
                            {
                                Position = worldSpaceP,
                                MaxDistance =
                                    max(worldSpaceBb.Extents.x, max(worldSpaceBb.Extents.y, worldSpaceBb.Extents.z)),
                                Filter = new CollisionFilter()
                                {
                                    BelongsTo = 1 << 0, // hmm
                                    CollidesWith = ~0u,
                                    GroupIndex = 0
                                }
                            };
                            physicsWorld.CalculateDistance(pointDistanceInput, out var closestHit);

                            if (closestHit.Distance <
                                0) // works for sphere collider and convex mesh, doesn't seem to work with capsule
                            {
                                if (voxelsBuffer.IsEmpty)
                                {
                                    voxelsBuffer = ecb.AddBuffer<VoxelElement>(entityInQueryIndex, entity);
                                }

                                voxelsBuffer.Add(p);
                            }
                        }

                        voxelizationMarker.End();
                    }
                }

                if (voxelsBuffer.IsEmpty)
                {
                    return; // voxels not created yet, can't calculate mass
                }

                Debug.Log($"Created {voxelsBuffer.Length} voxels");

                var volume = voxelsBuffer.Length * pow(voxelResolution, 3);
                var mass = volume * 700f *
                           0.0001f; //m3 * real density * multiplier // voxel of water is 1000kg but scale it down with a multiplier of 0.0001f 
                // pm = PhysicsMass.CreateDynamic(new MassProperties
                //     {
                //         MassDistribution = new MassDistribution()
                //         {
                //             Transform = pm.Transform,
                //             InertiaTensor = rcp(pm.InverseInertia) / mass
                //         },
                //         Volume = volume,
                //         AngularExpansionFactor = pm.AngularExpansionFactor
                //     },
                //     mass);
                Debug.Log($"Mass: {rcp(pm.InverseMass)}, Inertia: {rcp(pm.InverseInertia)}");
                ecb.SetComponent(entityInQueryIndex, entity, new PhysicsMass()
                {
                    Transform = pm.Transform,
                    AngularExpansionFactor = pm.AngularExpansionFactor,
                    InverseInertia = rcp(rcp(pm.InverseInertia) / rcp(pm.InverseMass) * mass), // hmm
                    InverseMass = rcp(mass),
                });
            }).Schedule();

        Dependency.Complete();
        _buildPhysicsWorld.AddInputDependencyToComplete(Dependency);

        var heightMarker = new ProfilerMarker("Height");
        var depthMarker = new ProfilerMarker("Depth");

        Entities
            // .WithoutBurst()
            .WithName("Apply_bouyancy")
            .WithReadOnly(physicsWorld)
            .ForEach((ref Translation translation, ref PhysicsVelocity pv, ref PhysicsDamping damping,
                ref BuoyantComponent buoyant,
                in Rotation rotation, in PhysicsMass pm, in PhysicsCollider col,
                in DynamicBuffer<VoxelElement> voxels) =>
            {
                // if (tick == 20)
                // {
                //     Profiler.enabled = true;
                //     ProfilerDriver.enabled = true;
                //     Debug.Log("Starting profiler");
                // }
                //
                // if (tick == 25)
                // {
                //     Profiler.enabled = false;
                //     ProfilerDriver.enabled = false;
                //     Debug.Log("Finishing profiler");
                //     Debug.Break();
                // }
                // GizmoManager.ClearGizmos();
                var waterHeightsCache = new NativeHashMap<float2, float>(voxels.Length, Allocator.Temp);
                var objectExtents = col.Value.Value.CalculateAabb(new RigidTransform(rotation.Value, translation.Value))
                    .Extents;

                var objectSizeForWaves = min(objectExtents.x, min(objectExtents.y, objectExtents.z));
                // Debug.Log($"{voxels.Length}");
                // Debug.Log($"Mass: {rcp(pm.InverseMass)}, Inertia: {rcp(pm.InverseInertia)}");
                // Debug.Log($"{tick}");

                var submergedVoxelsCount = 0;
                var sortedWavelengths = wavelengthBuffer.AsNativeArray();
                sortedWavelengths.Sort();
                var medianWavelength = sortedWavelengths[sortedWavelengths.Length / 2];

                var zeroPoint = new float3(10f, 10f, 10f);
                var zeroPoint2 = new float3(10f, 10f, 10f);

                // Debug.Log($"{_initialHeight}");

                if (tick == 40)
                {
                    var timeTest = 0f;
                    GerstnerHelpers.TryGetWaterHeight(
                        (float) timeTest,
                        ref zeroPoint,
                        objectSizeForWaves,
                        out var initialHeight,
                        spectrum.WindDirectionAngle,
                        spectrum.Chop,
                        spectrum.AttenuationInShallows,
                        physicsWorld,
                        wavelengthBuffer,
                        waveAmplitudeBuffer,
                        waveAngleBuffer,
                        phaseBuffer,
                        heightMarker,
                        depthMarker,
                        medianWavelength);
                    
                    // timeTest += 1f;

                    while (true)
                    {
                        timeTest += 1 / 60f;

                        GerstnerHelpers.TryGetWaterHeight(
                            (float) timeTest,
                            ref zeroPoint2,
                            objectSizeForWaves,
                            out var heightNow,
                            spectrum.WindDirectionAngle,
                            spectrum.Chop,
                            spectrum.AttenuationInShallows,
                            physicsWorld,
                            wavelengthBuffer,
                            waveAmplitudeBuffer,
                            waveAngleBuffer,
                            phaseBuffer,
                            heightMarker,
                            depthMarker,
                            medianWavelength);

                        if (abs(heightNow - initialHeight) < 0.0001f)
                        {
                            Debug.Log($"MATCH at time {timeTest}");
                            break;
                        }

                        if (timeTest % 30f < 0.1f)
                        {
                            Debug.Log($"{timeTest} passed");
                        }
                    }
                }

                for (int i = 0; i < voxels.Length; i++)
                {
                    var worldSpaceVoxel = transform(new RigidTransform(rotation.Value, translation.Value), voxels[i]);

                    var submerged = false;
                    var samplePoint = new float2(worldSpaceVoxel.x, worldSpaceVoxel.z);
                    float waterHeight;
                    if (waterHeightsCache.TryGetValue(samplePoint, out waterHeight) ||
                        GerstnerHelpers.TryGetWaterHeight(
                            (float) elapsedTime,
                            ref translation.Value,
                            objectSizeForWaves,
                            out waterHeight,
                            spectrum.WindDirectionAngle,
                            spectrum.Chop,
                            spectrum.AttenuationInShallows,
                            physicsWorld,
                            wavelengthBuffer,
                            waveAmplitudeBuffer,
                            waveAngleBuffer,
                            phaseBuffer,
                            heightMarker,
                            depthMarker,
                            medianWavelength))
                    {
                        waterHeightsCache[samplePoint] = waterHeight;
                        if (worldSpaceVoxel.y < waterHeight) // hmm
                        {
                            // GizmoManager.AddGizmoAction(() =>
                            // {
                            //     Gizmos.color = new Color(0, 1, 0, 1f);
                            //     Gizmos.DrawWireCube(worldSpaceVoxel, new float3(1.1f));
                            // });

                            submergedVoxelsCount++;
                            const float waterDensity = 1000f * 0.0001f; // real density * mass multiplier
                            var volumeOfDisplacedWater = pow(buoyant.VoxelResolution, 3);

                            var archimedesForce = new float3(
                                0f,
                                waterDensity * volumeOfDisplacedWater *
                                abs(PhysicsStep.Default.Gravity
                                    .y), // 1.1f is voxel res. 0.001f is the global mass multiplier I assumed
                                0f);

                            pm.GetImpulseFromForce(archimedesForce, ForceMode.Force, deltaTime, out var impulse,
                                out var impulseMass);

                            pv.ApplyImpulse(impulseMass, translation, rotation, impulse, worldSpaceVoxel);
                        }
                    }
                }

                // var ecb = _ghostSimulationSystemGroup.PostUpdateCommands.AsParallelWriter();
                var percentageSubmerged = submergedVoxelsCount / voxels.Length;
                var submergedFactor = lerp(buoyant.SubmergedPercentage, percentageSubmerged, 0.25f);
                buoyant.SubmergedPercentage = submergedFactor;
                var baseDampingLinear = 0.04f;
                var baseDampingAngular = 1f; //1.5f;
                damping = new PhysicsDamping()
                {
                    Linear = baseDampingLinear + baseDampingLinear * (percentageSubmerged * 10f),
                    Angular = baseDampingAngular + baseDampingAngular * (percentageSubmerged * 0.5f)
                };

                sortedWavelengths.Dispose();
            }).Schedule();

        _buildPhysicsWorld.AddInputDependencyToComplete(Dependency);
    }

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