using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Profiling;
using UnityEngine;
using Vermetio;

namespace DefaultNamespace
{
    [BurstCompile]
    public struct GetWaterHeightsJob : IJob
    {
        public float elapsedTime;
        public PhysicsWorld physicsWorld;
        public ProfilerMarker heightMarker;
        public ProfilerMarker depthMarker;
        public float medianWavelength;
        [ReadOnly] public NativeArray<float2> allVoxels;
        public NativeArray<GerstnerWaveComponent4> waveData;
        public NativeHashMap<float2, float> WaterHeightsPerPosition;

        public void Execute()
        {
            // var timeTest = 0f;
            // var tickTest = 0;
            // var waterHeightsInitial = GerstnerHelpers.GetWaterHeights(
            //     timeTest,
            //     allVoxels,
            //     physicsWorld,
            //     waveData,
            //     heightMarker,
            //     depthMarker,
            //     medianWavelength);
            //
            // var waterHeightsNow = new NativeArray<float>(waterHeightsInitial, Allocator.Temp);
            // timeTest += 1f;
            //
            // while (true)
            // {
            //     timeTest += 1 / 60f;
            //     tickTest++;
            //     
            //     waterHeightsNow = GerstnerHelpers.GetWaterHeights(
            //         timeTest,
            //         allVoxels,
            //         physicsWorld,
            //         waveData,
            //         heightMarker,
            //         depthMarker,
            //         medianWavelength);
            //
            //     var matchFound = true;
            //
            //     for (int i = 0; i < waterHeightsInitial.Length; i++)
            //     {
            //         if (math.abs(waterHeightsNow[i] - waterHeightsInitial[i]) > 0.001f)
            //         {
            //             matchFound = false;
            //             break;
            //         }
            //     }
            //
            //     if (matchFound)
            //     {
            //         Debug.Log($"MATCH at time {timeTest}, tick {tickTest}");
            //         break;
            //     }
            //
            //     if (timeTest > 100f)
            //         break;
            // }
            //
            // waterHeightsInitial.Dispose();
            // waterHeightsNow.Dispose();
            
            var waterHeights = GerstnerHelpers.GetWaterHeights(
                elapsedTime,
                allVoxels,
                physicsWorld,
                waveData,
                heightMarker,
                depthMarker,
                medianWavelength);

            for (int i = 0; i < allVoxels.Length; i++)
            {
                WaterHeightsPerPosition.TryAdd(allVoxels[i], waterHeights[i]);
            }
            
            waterHeights.Dispose();
        }
    }
}