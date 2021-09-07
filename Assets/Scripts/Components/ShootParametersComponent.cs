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
    public float MinimumShotDelay;
    
    public double Cooldown;
    
    public double LastShotFiredAt;

    [HideInInspector] 
    public bool TargetLegit;

    [HideInInspector] 
    public float3 Velocity;
}
