using System;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.NetCode;

[Serializable]
[GenerateAuthoringComponent]
[GhostComponent(PrefabType = GhostPrefabType.AllPredicted, OwnerSendType = SendToOwnerType.SendToOwner)]
public struct HealthComponent : IComponentData
{
    [GhostField]
    public int Value;
}
