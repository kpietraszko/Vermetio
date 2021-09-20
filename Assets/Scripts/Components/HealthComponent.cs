using System;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.NetCode;

[Serializable]
[GenerateAuthoringComponent]
[GhostComponent(PrefabType = GhostPrefabType.All)]
public struct HealthComponent : IComponentData
{
    [GhostField]
    public int Value;
}
