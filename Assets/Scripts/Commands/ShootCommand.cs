using System;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.NetCode;

[Serializable]
public struct ShootCommand : IRpcCommand
{
    public float3 TargetPosition;
}
