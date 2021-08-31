using System;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.NetCode;

[Serializable]
public struct BoatMouseInput : ICommandData
{
    public uint Tick { get; set; }
    public float3 AimPosition;
}