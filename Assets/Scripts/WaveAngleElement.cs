using System;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

[Serializable]
public struct WaveAngleElement : IBufferElementData
{
    public float Value;
    
    public static implicit operator float(WaveAngleElement angle) => angle.Value;
    public static implicit operator WaveAngleElement(float value) => new WaveAngleElement {Value = value};
}
