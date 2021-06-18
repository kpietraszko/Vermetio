using System;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.NetCode;

[Serializable]
public struct BoatInput : ICommandData
{
    public uint Tick { get; set; }
}
