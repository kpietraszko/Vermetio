using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Profiling;
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