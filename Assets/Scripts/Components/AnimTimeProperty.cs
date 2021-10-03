using System;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Rendering;

[GenerateAuthoringComponent]
[MaterialProperty("_AnimTime", MaterialPropertyFormat.Float)]
public struct AnimTimeProperty : IComponentData
{
    public float Value;
}
