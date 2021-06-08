using System.Runtime.CompilerServices;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.Profiling;

public class GerstnerHelpers
{
    // 🔥🔥 very hot path 🔥🔥
    public static bool TryGetWaterHeight(float elapsedTime,
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
        DynamicBuffer<PhaseElement> phaseBuffer,
        ProfilerMarker heightMarker,
        ProfilerMarker depthMarker, 
        float medianWavelength)
    {
        heightMarker.Begin();
        // FPI - guess should converge to location that displaces to the target position
        var guess = worldPos;
        // 2 iterations was enough to get very close when chop = 1, added 2 more which should be
        // sufficient for most applications. for high chop values or really stormy conditions there may
        // be some error here. one could also terminate iteration based on the size of the error, this is
        // worth trying but is left as future work for now.
        float3 disp;
        for (int i = 0;
            i < 3 && SampleDisplacement(
                elapsedTime,
                ref worldPos,
                minSpatialLength,
                out disp,
                windDirAngle,
                chop,
                attenuationInShallows,
                physicsWorld,
                wavelengthBuffer,
                waveAmplitudeBuffer,
                waveAngleBuffer,
                phaseBuffer, 
                depthMarker, 
                medianWavelength);
            i++)
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
            minSpatialLength,
            out var displacement,
            windDirAngle,
            chop,
            attenuationInShallows,
            physicsWorld,
            wavelengthBuffer,
            waveAmplitudeBuffer,
            waveAngleBuffer,
            phaseBuffer, 
            depthMarker, 
            medianWavelength))
        {
            height = default;
            heightMarker.End();
            return false;
        }

        height = 0f + displacement.y; // 0 is hard coded sea level
        heightMarker.End();
        return true;
    }

    // 🔥🔥🔥 very very hot path 🔥🔥🔥
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
        DynamicBuffer<PhaseElement> phaseBuffer,
        ProfilerMarker depthMarker, 
        float medianWavelength)
    {
        displacement = new float3();

        if (waveAmplitudeBuffer.IsEmpty)
        {
            return false;
        }

        var pos = new float2(worldPos.x, worldPos.z);
        float windAngle = windDirAngle;
        float minWavelength = minSpatialLength / 2f;
        depthMarker.Begin();
        var weight = GetAttenuatedWeight(pos, attenuationInShallows, wavelengthBuffer, physicsWorld, medianWavelength);
        depthMarker.End();

        for (int j = 0; j < waveAmplitudeBuffer.Length; j++)
        {
            if (waveAmplitudeBuffer[j] <= 0.001f) continue;
            if (wavelengthBuffer[j] < minWavelength) continue;

            float C = ComputeWaveSpeed(wavelengthBuffer[j]);

            // direction
            var D = new float2(math.cos((math.radians(windAngle + waveAngleBuffer[j]))),
                math.sin((math.radians(windAngle + waveAngleBuffer[j]))));
            // wave number
            float k = 2f * math.PI / wavelengthBuffer[j];

            float x = math.dot(D, pos);
            float t = k * (x + C * elapsedTime) + phaseBuffer[j];
            float disp = -chop * math.sin(t);
            displacement += waveAmplitudeBuffer[j] * new float3(
                D.x * disp,
                math.cos(t),
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
        PhysicsWorld physicsWorld,
        float medianWavelength)
    {
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
        float depth = math.abs(0f + raycastHit.Position.y); // 0f is hardcoded sea level;
        var maxDepth = 100f;
        var depthNormalized = depth / maxDepth;
        var depth_wt = math.saturate(depthNormalized * medianWavelength / math.PI);
        return attenuationInShallows * depth_wt + (1.0f - attenuationInShallows);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static float ComputeWaveSpeed(float wavelength /*, float depth*/)
    {
        // wave speed of deep sea ocean waves: https://en.wikipedia.org/wiki/Wind_wave
        // https://en.wikipedia.org/wiki/Dispersion_(water_waves)#Wave_propagation_and_dispersion
        float g = 9.81f;
        float k = 2f * math.PI / wavelength;
        //float h = max(depth, 0.01);
        //float cp = sqrt(abs(tanh_clamped(h * k)) * g / k);
        float cp = math.sqrt(g / k);
        return cp;
    }
}