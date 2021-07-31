using System;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

[GenerateAuthoringComponent]
public struct SimpleBuoyantComponent : IComponentData
{
    public float RaiseObject; // = 1f;
    public float BuoyancyCoeff; // = 3f;
    public float AccelerateDownhill; // = 0f;
    public float ForceHeightOffset; // = -0.3f;
    public float DragInWaterUp; // = 3f; 
    public float DragInWaterRight; // = 2f;
    public float DragInWaterForward; // = 1f;
    public float BouyancyTorque; // = 8f;
    public float DragInWaterRotational; // = 0.2f;
}
