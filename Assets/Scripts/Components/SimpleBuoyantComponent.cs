using System;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

[GenerateAuthoringComponent]
public struct SimpleBuoyantComponent : IComponentData
{
    [Tooltip("Offsets center of object to raise it (or lower it) in the water.")]
    public float RaiseObject; // = 1f;
    
    [Tooltip("Strength of buoyancy force per meter of submersion in water.")]
    public float BuoyancyCoeff; // = 3f;
    
    [Tooltip("Approximate hydrodynamics of 'surfing' down waves."), Range(0, 1)]
    public float AccelerateDownhill; // = 0f;
    
    [Tooltip("Vertical offset for where drag force should be applied.")]
    public float ForceHeightOffset; // = -0.3f;
    
    public float DragInWaterUp; // = 3f; 
    public float DragInWaterRight; // = 2f;
    public float DragInWaterForward; // = 1f;
    
    [Tooltip("Strength of torque applied to match boat orientation to water normal.")]
    public float BouyancyTorque; // = 8f;
    public float DragInWaterRotational; // = 0.2f;
}
