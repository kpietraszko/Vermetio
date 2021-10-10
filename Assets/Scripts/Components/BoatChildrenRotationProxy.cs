using System;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.NetCode;
using UnityEngine;

/// <summary>
/// The rotation is calculated into this component (on Player entity) and then applied from this to actual child rotation
/// on both server and client.
/// This is needed because SendDataForChildEntity is critically bugged in 0.6 and tends to break whole deserialization
/// </summary>
[Serializable]
[GenerateAuthoringComponent]
[GhostComponent]
public struct BoatChildrenRotationProxy : IComponentData
{
    [GhostField]
    // [HideInInspector]
    public quaternion AxleRotation;
}
