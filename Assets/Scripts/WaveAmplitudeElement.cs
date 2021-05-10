using System;
using Unity.Collections;
using Unity.Entities;
using UnityEngine.Rendering;

[Serializable]
public struct WaveAmplitudeElement : IBufferElementData
{
    private float Value;

    public static implicit operator float(WaveAmplitudeElement amplitude) => amplitude.Value;

    public static implicit operator WaveAmplitudeElement(float value) => new WaveAmplitudeElement {Value = value};
}