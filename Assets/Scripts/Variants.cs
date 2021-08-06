using System.Collections;
using System.Collections.Generic;
using Unity.Mathematics;
using Unity.NetCode;
using Unity.Transforms;
using UnityEngine;

[GhostComponentVariation(typeof(Translation), "Translation - Low precision")]
[GhostComponent(PrefabType = GhostPrefabType.All, OwnerPredictedSendType = GhostSendType.All, SendDataForChildEntity = false)]
public struct TranslationLowPrec
{
    // Default quantization is 100
    [GhostField(Quantization=10, Smoothing=SmoothingAction.InterpolateAndExtrapolate, SubType=SubTypes.Float3_LowPrec)] public float3 Value;
}

[GhostComponentVariation(typeof(Rotation), "Rotation - Low precision")]
[GhostComponent(PrefabType = GhostPrefabType.All, OwnerPredictedSendType = GhostSendType.All, SendDataForChildEntity = false)]
public struct RotationLowPrec
{
    // Default quantization is 1000
    [GhostField(Quantization=100, Smoothing=SmoothingAction.InterpolateAndExtrapolate, SubType=SubTypes.Rot_LowPrec)] public quaternion Value;
}


