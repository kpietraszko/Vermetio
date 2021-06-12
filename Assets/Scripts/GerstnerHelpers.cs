using System.Runtime.CompilerServices;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.Profiling;

namespace Vermetio
{
    public class GerstnerHelpers
    {
        // 🔥🔥 very hot path 🔥🔥
        public static NativeArray<float> GetWaterHeights(float elapsedTime,
            NativeArray<float2> worldPositions,
            float minSpatialLength,
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
            float medianWavelength, 
            float smallestWavelength)
        {
            heightMarker.Begin();
            // FPI - guess should converge to location that displaces to the target position
            var guesses = new NativeArray<float3>(worldPositions.Length, Allocator.Temp);
            var positions = new NativeArray<float2>(worldPositions.Length, Allocator.Temp,
                NativeArrayOptions.UninitializedMemory);

            for (int i = 0; i < guesses.Length; i++)
            {
                guesses[i] = new float3(worldPositions[i].x, 0f, worldPositions[i].y);
            }

            var smallestWavespeed = ComputeWaveSpeed(smallestWavelength);

            // 2 iterations was enough to get very close when chop = 1, added 2 more which should be
            // sufficient for most applications. for high chop values or really stormy conditions there may
            // be some error here. one could also terminate iteration based on the size of the error, this is
            // worth trying but is left as future work for now.
            // var disp = new NativeArray<float3>(worldPositions.Length, Allocator.Temp);
            for (int step = 0; step < 4; step++)
            {
                var disp = SampleDisplacements(
                    elapsedTime,
                    positions,
                    minSpatialLength,
                    windDirAngle,
                    chop,
                    attenuationInShallows,
                    physicsWorld,
                    wavelengthBuffer,
                    waveAmplitudeBuffer,
                    waveAngleBuffer,
                    phaseBuffer,
                    depthMarker,
                    medianWavelength, 
                    smallestWavespeed);

                for (int i = 0; i < positions.Length; i++)
                {
                    var guess = guesses[i];
                    var error = guess + disp[i] -
                                new float3(worldPositions[i].x, 0f,
                                    worldPositions[i]
                                        .y); // not sure about the worldPositions, y shouldn't matter ...I think
                    guesses[i] = new float3(guess.x - error.x, guess.y, guess.z - error.z);
                }
            }

            for (int i = 0; i < positions.Length; i++)
            {
                positions[i] = new float2(guesses[i].x, guesses[i].z);
            }


            var displacements = SampleDisplacements(
                elapsedTime,
                positions,
                minSpatialLength,
                windDirAngle,
                chop,
                attenuationInShallows,
                physicsWorld,
                wavelengthBuffer,
                waveAmplitudeBuffer,
                waveAngleBuffer,
                phaseBuffer,
                depthMarker,
                medianWavelength, 
                smallestWavespeed);

            var heights = new NativeArray<float>(displacements.Length, Allocator.Temp,
                NativeArrayOptions.UninitializedMemory);
            for (int i = 0; i < heights.Length; i++)
            {
                heights[i] = 0f + displacements[i].y; // 0 is hard coded sea level
            }

            heightMarker.End();
            guesses.Dispose();
            positions.Dispose();

            return heights;
        }

        // 🔥🔥🔥 very very hot path 🔥🔥🔥
        private static NativeArray<float3> SampleDisplacements(
            float elapsedTime,
            NativeArray<float2> positions,
            float minSpatialLength,
            float windDirAngle,
            float chop,
            float attenuationInShallows,
            PhysicsWorld physicsWorld,
            DynamicBuffer<WavelengthElement> wavelengthBuffer,
            DynamicBuffer<WaveAmplitudeElement> waveAmplitudeBuffer,
            DynamicBuffer<WaveAngleElement> waveAngleBuffer,
            DynamicBuffer<PhaseElement> phaseBuffer,
            ProfilerMarker depthMarker,
            float medianWavelength,
            float smallestWaveSpeed)
        {
            var displacements = new NativeArray<float3>(positions.Length, Allocator.Temp);
            float windAngle = windDirAngle;
            float minWavelength = minSpatialLength / 2f;
            // depthMarker.Begin();
            var
                weight = 1f; //GetAttenuatedWeight(pos, attenuationInShallows, wavelengthBuffer, physicsWorld, medianWavelength);
            // depthMarker.End();

            for (int j = 0; j < waveAmplitudeBuffer.Length; j++)
            {
                var wavelength = wavelengthBuffer[j];
                if (waveAmplitudeBuffer[j] <= 0.001f) continue;
                if (wavelength < minWavelength) continue;

                // float C = QuantizeWaveSpeed(ComputeWaveSpeed(wavelength), smallestWaveSpeed);
                float C = QuantizeWaveSpeed(ComputeWaveSpeed(wavelength), smallestWaveSpeed);

                // direction
                var D = new float2(math.cos((math.radians(windAngle + waveAngleBuffer[j]))),
                    math.sin((math.radians(windAngle + waveAngleBuffer[j]))));
                // wave number
                float k = 2f * math.PI / wavelength;

                for (int posIndex = 0; posIndex < positions.Length; posIndex++)
                {
                    float x = math.dot(D, positions[posIndex]);
                    float t = k * (x + C * elapsedTime) + phaseBuffer[j];
                    float disp = -chop * math.sin(t);
                    displacements[posIndex] += waveAmplitudeBuffer[j] * new float3(
                        D.x * disp,
                        math.cos(t),
                        D.y * disp
                    );
                }
            }

            for (int posIndex = 0; posIndex < positions.Length; posIndex++)
            {
                displacements[posIndex] *= weight;
            }

            return displacements;
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
            float k = 2f * math.PI / wavelength;
            //float h = max(depth, 0.01);
            //float cp = sqrt(abs(tanh_clamped(h * k)) * g / k);
            return math.sqrt(9.81f / k);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float QuantizeWaveSpeed(float waveSpeed, float smallestWaveSpeed)
        {
            return ((int) (waveSpeed / smallestWaveSpeed)) * smallestWaveSpeed;
        }
    }
}