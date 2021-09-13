using System;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.NetCode;

[Serializable]
[GenerateAuthoringComponent]
[GhostComponent(PrefabType = GhostPrefabType.AllPredicted, OwnerPredictedSendType = GhostSendType.Predicted)]
public struct PlayerInventoryComponent : IComponentData
{
    [GhostField]
    public int Coconuts;
}
