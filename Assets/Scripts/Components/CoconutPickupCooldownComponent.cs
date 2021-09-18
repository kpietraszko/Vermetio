using System;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

[Serializable]
public struct CoconutPickupCooldownComponent : IComponentData
{
    public double CooldownStartedAt;
}
