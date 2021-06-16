using System.Collections;
using System.Collections.Generic;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

namespace Vermetio
{
    public static class DebugDraw
    {
        public static void DrawCross(this float3 point, float size, Color color, float durationS)
        {
            Debug.DrawLine(point - new float3(size / 2f, 0f, 0f), point + new float3(size / 2f, 0f, 0f), color, durationS);
            Debug.DrawLine(point - new float3(0f, size / 2f, 0f), point + new float3(0f, size / 2f, 0f), color, durationS);
            Debug.DrawLine(point - new float3(0f, 0f, size / 2f), point + new float3(0f, 0f, size / 2f), color, durationS);
        }
    }
}