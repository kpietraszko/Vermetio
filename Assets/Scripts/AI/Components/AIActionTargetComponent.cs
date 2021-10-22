using System;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

[Serializable]
public struct AIActionTargetComponent : IComponentData
{
    public Entity Value;
}
