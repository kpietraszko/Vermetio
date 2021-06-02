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

        // string docPath =
        //     Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        //
        // using (StreamWriter outputFile = new StreamWriter(Path.Combine(docPath, "BuoyancyElapsedTime.csv"), append: true))
        // {
        //     outputFile.WriteLine($"{DateTime.Now.Ticks};{elapsedTime}");
        // }

        var physicsWorld = _buildPhysicsWorld.PhysicsWorld;

        Dependency = JobHandle.CombineDependencies(Dependency, _endFramePhysics.GetOutputDependency());
        
        var heightMarker = new ProfilerMarker("Height");
        var voxelizationMarker = new ProfilerMarker(("Voxelization"));

        Entities
            // .WithoutBurst()
            .WithReadOnly(physicsWorld)
            .ForEach((ref Translation translation, ref PhysicsVelocity pv, in Rotation rotation,
                in LocalToWorld localToWorld,
                in PhysicsMass pm, in PhysicsCollider col,
                in BuoyantComponent buoyant) =>
            {
                // GizmoManager.ClearGizmos();

                var voxels = new NativeList<float3>(Allocator.Temp); // not used for now
                var submergedVoxels = new NativeList<float3>(Allocator.Temp);
                var voxelResolution = 1.1f; // represents the half size? of a voxel when creating the voxel representation
                var boundingBox = col.Value.Value.CalculateAabb();
                var rigidTransform = new RigidTransform(rotation.Value, translation.Value);
                var worldSpaceBb = Unity.Physics.Math.TransformAabb(rigidTransform, boundingBox);
                // DrawBounds(new Bounds(worldSpaceBb.Center, worldSpaceBb.Extents), 1 / 60f);
                // Debug.Log($"{worldSpaceBb.Min}");
                for (var ix = worldSpaceBb.Min.x; ix < worldSpaceBb.Max.x; ix += voxelResolution)
                {
                    for (var iz = worldSpaceBb.Min.z; iz < worldSpaceBb.Max.z; iz += voxelResolution)
                    {
                        heightMarker.Begin();
                        if (!GerstnerHelpers.TryGetWaterHeight( // water height doesn't depend on the y of the voxel
                            (float) elapsedTime,
                            ref translation.Value,
                            0.5f,
                            out var waterHeight,
                            spectrum.WindDirectionAngle,
                            spectrum.Chop,
                            spectrum.AttenuationInShallows,
                            physicsWorld,
                            wavelengthBuffer,
                            waveAmplitudeBuffer,
                            waveAngleBuffer,
                            phaseBuffer))
                        {
                            // TODO: handle failure?
                        }

                        heightMarker.End();
                        
                        voxelizationMarker.Begin();
                        for (var iy = worldSpaceBb.Min.y; iy < worldSpaceBb.Max.y; iy += voxelResolution)
                        {
                            var x = (voxelResolution * 0.5f) + ix;
                            var y = (voxelResolution * 0.5f) + iy;
                            var z = (voxelResolution * 0.5f) + iz;

                            var p = new float3(x, y, z); // + worldSpaceBb.Center;

                            // TODO: check if p is inside collider

                            var pointDistanceInput = new PointDistanceInput()
                            {
                                Position = p,
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
                            if (closestHit.Distance < 0) // works for sphere collider and convex mesh, doesn't seem to work with capsule
                            {
                                voxels.Add(p); // add voxel if it's inside collider
                                if (p.y + 0.5f * voxelResolution < waterHeight) // hmm
                                {
                                    submergedVoxels.Add(p);
                                    // GizmoManager.AddGizmoAction(() =>
                                    // {
                                    //     Gizmos.color = new Color(0, 1, 0, 1f);
                                    //     Gizmos.DrawWireCube(p, new float3(voxelResolution));
                                    // });
                                }
                            }
                        }
                        voxelizationMarker.End();
                    }
                }
                
                var voxelVolume = Mathf.Pow(voxelResolution, 3f) * voxels.Length;
                var rawVolume = worldSpaceBb.Extents.x * worldSpaceBb.Extents.y * worldSpaceBb.Extents.z;
                var volume = Mathf.Min(rawVolume, voxelVolume);
                var density = rcp(pm.InverseMass) / volume;

                const float waterDensity = 1f;

                var archimedesForce = new float3(
                    0f,
                    waterDensity * pow(voxelResolution, 3) * abs(PhysicsStep.Default.Gravity.y),
                    0f);
                
                pm.GetImpulseFromForce(archimedesForce, ForceMode.Acceleration, deltaTime, out var impulse, out var impulseMass);

                for (int i = 0; i < submergedVoxels.Length; i++)
                {
                    pv.ApplyImpulse(impulseMass, translation, rotation, impulse, submergedVoxels[i]);
                }
                // translation.Value.y = displacement;
                // pv.ApplyImpulse(pm, translation, rotation, new float3(0f, 10f * deltaTime, 0f), translation.Value);
                voxels.Dispose();
                submergedVoxels.Dispose();
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