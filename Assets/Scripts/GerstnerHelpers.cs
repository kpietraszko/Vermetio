using System.Runtime.CompilerServices;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.Profiling;
using static Unity.Mathematics.math;

namespace Vermetio
{
    public class GerstnerHelpers
    {
        // 🔥🔥 hot path 🔥🔥
        public static NativeArray<float> GetWaterHeights(float elapsedTime,
            NativeArray<float2> worldPositions,
            PhysicsWorld physicsWorld, 
            NativeArray<GerstnerWaveComponent4> waveData, 
            ProfilerMarker heightMarker,
            ProfilerMarker depthMarker,
            float medianWavelength)
        {
            heightMarker.Begin();
            // FPI - guess should converge to location that displaces to the target position
            var guesses = new NativeArray<float3>(worldPositions.Length, Allocator.Temp);
            var undisplacedPositions = new NativeArray<float2>(worldPositions, Allocator.Temp);

            for (int i = 0; i < guesses.Length; i++)
            {
                guesses[i] = new float3(worldPositions[i].x, 0f, worldPositions[i].y);
            }

            // 2 iterations was enough to get very close when chop = 1, added 2 more which should be
            // sufficient for most applications. for high chop values or really stormy conditions there may
            // be some error here. one could also terminate iteration based on the size of the error, this is
            // worth trying but is left as future work for now.
            // var disp = new NativeArray<float3>(worldPositions.Length, Allocator.Temp);
            for (int step = 0; step < 4; step++)
            {
                var disp = SampleDisplacements(
                    elapsedTime,
                    worldPositions, 
                    waveData, 
                    physicsWorld, 
                    depthMarker, 
                    medianWavelength);

                for (int i = 0; i < worldPositions.Length; i++)
                {
                    var guess = guesses[i];
                    var error = guess + disp[i] -
                                new float3(worldPositions[i].x, 0f, worldPositions[i].y); // not sure about the worldPositions, y shouldn't matter ...I think
                    guesses[i] = new float3(guess.x - error.x, guess.y, guess.z - error.z);
                }
            }

            for (int i = 0; i < undisplacedPositions.Length; i++)
            {
                undisplacedPositions[i] = new float2(guesses[i].x, guesses[i].z);
            }


            var displacements = SampleDisplacements(
                elapsedTime,
                undisplacedPositions, 
                waveData, 
                physicsWorld,
                depthMarker,
                medianWavelength);

            var heights = new NativeArray<float>(displacements.Length, Allocator.Temp,
                NativeArrayOptions.UninitializedMemory);
            for (int i = 0; i < heights.Length; i++)
            {
                heights[i] = 0f + displacements[i].y; // 0 is hard coded sea level
            }

            
            guesses.Dispose();
            undisplacedPositions.Dispose();
            heightMarker.End();

            return heights;
        }

        // 🔥🔥🔥 hot path 🔥🔥🔥
        private static NativeArray<float3> SampleDisplacements(
            float elapsedTime,
            NativeArray<float2> positions,
            NativeArray<GerstnerWaveComponent4> waveData,
            PhysicsWorld physicsWorld,
            ProfilerMarker depthMarker,
            float medianWavelength)
        {
            var results = new NativeArray<float3>(positions.Length, Allocator.Temp);
            for (int fourComponentsIndex = 0; fourComponentsIndex < waveData.Length; fourComponentsIndex++)
            {
                var data = waveData[fourComponentsIndex];
                // direction
                float4 Dx = data._waveDirX;
                float4 Dz = data._waveDirZ;

                // wave number
                float4 k = data._twoPiOverWavelength;

                float4 kx = k * Dx;
                float4 kz = k * Dz;

                for (int positionIndex = 0; positionIndex < positions.Length; positionIndex++)
                {
                    // spatial location
                    float4 x = kx * positions[positionIndex].x + kz * positions[positionIndex].y;
                    float4 angle = data._phase - data._omega * elapsedTime; // omega was calculated using C that's already quantized in ShapeGerstner.cs
                    angle += x;

                    // dx and dz could be baked into _ChopAmp
                    float4 disp = data._chopAmp * sin(angle);
                    float4 resultx = disp * Dx;
                    float4 resultz = disp * Dz;

                    float4 resulty = data._amp * cos(angle);

                    // sum the vector results
                    results[positionIndex] += new float3(
                        dot(resultx, 1.0f), 
                        dot(resulty, 1.0f), 
                        dot(resultz, 1.0f));
                }
            }
            return results;
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
    }
}