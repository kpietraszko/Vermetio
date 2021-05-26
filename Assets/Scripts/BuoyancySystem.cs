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
using Unity.Physics.Systems;
using Unity.Transforms;
using UnityEngine;

[UpdateInGroup(typeof(GhostPredictionSystemGroup))]
[UpdateAfter(typeof(ExportPhysicsWorld))]
// [UpdateBefore(typeof(EndFramePhysicsSystem))]
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
        
        // string docPath =
        //     Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        //
        // using (StreamWriter outputFile = new StreamWriter(Path.Combine(docPath, "BuoyancyElapsedTime.csv"), append: true))
        // {
        //     outputFile.WriteLine($"{DateTime.Now.Ticks};{elapsedTime}");
        // }

        var physicsWorld = _buildPhysicsWorld.PhysicsWorld;

        Dependency = JobHandle.CombineDependencies(Dependency, _endFramePhysics.GetOutputDependency());
        
        Entities
            .WithReadOnly(physicsWorld)
            .ForEach((ref Translation translation, in Rotation rotation, in BuoyantComponent buoyant) =>
        {
            if (!SampleDisplacement(
                (float) elapsedTime, 
                ref translation.Value,
                0.5f,
                out var displacement,
                spectrum.WindDirectionAngle,
                spectrum.Chop,
                spectrum.AttenuationInShallows, 
                physicsWorld, 
                wavelengthBuffer,
                waveAmplitudeBuffer,
                waveAngleBuffer,
                phaseBuffer))
            {
                
            }
            // if (elapsedTime < 1f)
            //     Debug.Log($"{translation.Value}");

            translation.Value.y = displacement.y;
        }).Schedule();
        
        _buildPhysicsWorld.AddInputDependencyToComplete(Dependency);
    }

    private static bool TryGetWaterHeight(
        float elapsedTime,
        ref float3 worldPos,
        float minSpatialLength,
        out float height,
        float windDirAngle,
        float chop,
        float attenuationInShallows, 
        PhysicsWorld physicsWorld, 
        DynamicBuffer<WavelengthElement> wavelengthBuffer,
        DynamicBuffer<WaveAmplitudeElement> waveAmplitudeBuffer,
        DynamicBuffer<WaveAngleElement> waveAngleBuffer,
        DynamicBuffer<PhaseElement> phaseBuffer)
    {
        // FPI - guess should converge to location that displaces to the target position
        var guess = worldPos;
        // 2 iterations was enough to get very close when chop = 1, added 2 more which should be
        // sufficient for most applications. for high chop values or really stormy conditions there may
        // be some error here. one could also terminate iteration based on the size of the error, this is
        // worth trying but is left as future work for now.
        float3 disp;
        for (int i = 0; i < 4 && SampleDisplacement(
            elapsedTime,
            ref worldPos,
            0.1f,
            out disp,
            windDirAngle,
            chop, 
            attenuationInShallows, 
            physicsWorld, 
            wavelengthBuffer,
            waveAmplitudeBuffer,
            waveAngleBuffer,
            phaseBuffer); i++)
        {
            var error = guess + disp - worldPos;
            guess.x -= error.x;
            guess.z -= error.z;
        }

        var undisplacedWorldPos = guess;
        undisplacedWorldPos.y = 0f; // hardcoded sea level

        if (!SampleDisplacement(
            elapsedTime,
            ref undisplacedWorldPos,
            0.1f,
            out var displacement,
            windDirAngle,
            chop, 
            attenuationInShallows, 
            physicsWorld, 
            wavelengthBuffer,
            waveAmplitudeBuffer,
            waveAngleBuffer,
            phaseBuffer))
        {
            height = default;
            return false;
        }
        
        height = 0f + displacement.y; // 0 is hard coded sea level
        return true;
    }
    
    private static bool SampleDisplacement(
        float elapsedTime, 
        ref float3 worldPos, // not sure why this is ref
        float minSpatialLength,
        out float3 displacement,
        float windDirAngle,
        float chop,
        float attenuationInShallows, 
        PhysicsWorld physicsWorld, 
        DynamicBuffer<WavelengthElement> wavelengthBuffer,
        DynamicBuffer<WaveAmplitudeElement> waveAmplitudeBuffer,
        DynamicBuffer<WaveAngleElement> waveAngleBuffer,
        DynamicBuffer<PhaseElement> phaseBuffer)
    {
        displacement = new float3();

        if (waveAmplitudeBuffer.IsEmpty || !waveAmplitudeBuffer.IsCreated)
        {
            return false;
        }

        var pos = new float2(worldPos.x, worldPos.z);
        float windAngle = windDirAngle;
        float minWavelength = minSpatialLength / 2f;
        var weight = GetAttenuatedWeight(pos, attenuationInShallows, wavelengthBuffer, physicsWorld);

        for (int j = 0; j < waveAmplitudeBuffer.Length; j++)
        {
            if (waveAmplitudeBuffer[j] <= 0.001f) continue;
            if (wavelengthBuffer[j] < minWavelength) continue;

            float C = ComputeWaveSpeed(wavelengthBuffer[j]);

            // direction
            var D = new float2(cos((radians(windAngle + waveAngleBuffer[j]))),
                sin((radians(windAngle + waveAngleBuffer[j]))));
            // wave number
            float k = 2f * PI / wavelengthBuffer[j];

            float x = dot(D, pos);
            float t = k * (x + C * elapsedTime) + phaseBuffer[j];
            float disp = -chop * sin(t);
            displacement += waveAmplitudeBuffer[j] * new float3(
                D.x * disp,
                cos(t),
                D.y * disp
            );
        }

        displacement *= weight;

        return true;
    }

    private static float GetAttenuatedWeight(
        float2 worldPos,  
        float attenuationInShallows, 
        DynamicBuffer<WavelengthElement> wavelengthBuffer, 
        PhysicsWorld physicsWorld)
    {
        var sortedWavelengths = wavelengthBuffer.AsNativeArray();
        sortedWavelengths.Sort();
        var medianWavelength = sortedWavelengths[sortedWavelengths.Length / 2];
        
        var rayStart = new float3(worldPos.x, 0f, worldPos.y);
        var raycastInput = new RaycastInput()
        {
            Start = rayStart, // 0f is hardcoded sea level
            End = rayStart - new float3(0f, 256f, 0f),
            Filter = new CollisionFilter()
            {
                BelongsTo = 1 << 1,
                CollidesWith = 1 << 0,
            }
        };

        physicsWorld.CastRay(raycastInput, out var raycastHit);
        Debug.Log($"position: {raycastHit.Position}");
        float depth = abs(0f + raycastHit.Position.y); // 0f is hardcoded sea level;
        Debug.Log($"{depth}");
        var maxDepth = 100f;
        var depthNormalized = depth / maxDepth;
        Debug.Log($"{depthNormalized}");
        var depth_wt = saturate(depthNormalized * medianWavelength / PI);
        sortedWavelengths.Dispose();
        return attenuationInShallows * depth_wt + (1.0f - attenuationInShallows);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static float ComputeWaveSpeed(float wavelength /*, float depth*/)
    {
        // wave speed of deep sea ocean waves: https://en.wikipedia.org/wiki/Wind_wave
        // https://en.wikipedia.org/wiki/Dispersion_(water_waves)#Wave_propagation_and_dispersion
        float g = 9.81f;
        float k = 2f * PI / wavelength;
        //float h = max(depth, 0.01);
        //float cp = sqrt(abs(tanh_clamped(h * k)) * g / k);
        float cp = sqrt(g / k);
        return cp;
    }
}