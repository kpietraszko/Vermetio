using System;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.NetCode;

[GenerateAuthoringComponent]
[GhostComponent(PrefabType = GhostPrefabType.All)]
public struct PlayerInventoryComponent : IComponentData
{
    [GhostField]
    public int Coconuts;
}
