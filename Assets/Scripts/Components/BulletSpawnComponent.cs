using System;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

[Serializable]
public struct BulletSpawnComponent : IComponentData
{
    public long SpawnAtTick;
    public float3 Velocity;
}
