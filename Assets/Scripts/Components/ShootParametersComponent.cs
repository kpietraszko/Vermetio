using System;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Serialization;

[Serializable]
[GenerateAuthoringComponent]
public struct ShootParametersComponent : IComponentData
{
    [HideInInspector] 
    public bool Legit;

    [HideInInspector] 
    public float3 Velocity;
}
