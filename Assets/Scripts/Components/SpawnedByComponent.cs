using System;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

[Serializable]
public struct SpawnedByComponent : IComponentData
{
    public Entity Player;
}
