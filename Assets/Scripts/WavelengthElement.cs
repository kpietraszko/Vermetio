using System;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

[Serializable]
public struct WavelengthElement : IBufferElementData
{
    public float Value;
    
    public static implicit operator float(WavelengthElement wavelength) => wavelength.Value;
    public static implicit operator WavelengthElement(float value) => new WavelengthElement {Value = value};
}
