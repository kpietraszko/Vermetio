using System;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

[Serializable]
public struct PhaseElement : IBufferElementData
{
    public float Value;
    
    public static implicit operator float(PhaseElement phase) => phase.Value;
    public static implicit operator PhaseElement(float value) => new PhaseElement {Value = value};
}
