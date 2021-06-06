using System;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

[GenerateAuthoringComponent]
public struct BuoyantComponent : IComponentData
{
    [HideInInspector]
    public float VoxelResolution;
    [HideInInspector]
    public float SubmergedPercentage;
}
