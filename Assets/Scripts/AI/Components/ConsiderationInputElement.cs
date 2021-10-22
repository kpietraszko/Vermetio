using System;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Vermetio.AI;

[Serializable]
[InternalBufferCapacity((int)ConsiderationInputType.Count)]
public struct ConsiderationInputElement : IBufferElementData
{
    public float Value;

    public static implicit operator ConsiderationInputElement(float value) =>
        new ConsiderationInputElement() { Value = value };
}
