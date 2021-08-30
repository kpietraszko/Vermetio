using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using Unity.Collections;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace Vermetio
{
    public static class DebugDraw
    {
        [Conditional("UNITY_EDITOR")]
        public static void DrawCross(this float3 point, float size, Color color, float durationS)
        {
            #if UNITY_EDITOR
            Debug.DrawLine(point - new float3(size / 2f, 0f, 0f), point + new float3(size / 2f, 0f, 0f), color, durationS);
            Debug.DrawLine(point - new float3(0f, size / 2f, 0f), point + new float3(0f, size / 2f, 0f), color, durationS);
            Debug.DrawLine(point - new float3(0f, 0f, size / 2f), point + new float3(0f, 0f, size / 2f), color, durationS);
            #endif
        }

        public static NativeArray<float3x2> GetCross(this float3 point, float size)
        {
            var result = new NativeArray<float3x2>(3, Allocator.TempJob);
            result[0] = new float3x2(point - new float3(size / 2f, 0f, 0f), point + new float3(size / 2f, 0f, 0f));
            result[1] = new float3x2(point - new float3(0f, size / 2f, 0f), point + new float3(0f, size / 2f, 0f));
            result[2] = new float3x2(point - new float3(0f, 0f, size / 2f), point + new float3(0f, 0f, size / 2f));
            return result;
        }
    }
}