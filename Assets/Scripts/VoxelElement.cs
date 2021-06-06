using System;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

[Serializable]
public struct VoxelElement : IBufferElementData
{
    public float3 Value;
    
    public static implicit operator float3(VoxelElement angle) => angle.Value;
    public static implicit operator VoxelElement(float3 value) => new VoxelElement {Value = value};
}