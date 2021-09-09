using System;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

[Serializable]
[GenerateAuthoringComponent]
public struct CoconutSpawnCooldownComponent : IComponentData
{
    // [HideInInspector]
    public double CooldownStartedAt;
}
