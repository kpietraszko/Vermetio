
using System;
using Crest;
using Unity.Entities;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Serialization;

public struct ProbyBuoyantComponent : IComponentData
{
    public float3 CenterOfMass;
    public float ForceHeightOffset;
    public float ForceMultiplier;
    public float MinSpatialLength;
    public float TurningHeel;
    
    public float DragInWaterUp;
    public float DragInWaterRight;
    public float DragInWaterForward;
    public float EnginePower;
    public float TurnPower;
    public float EngineBias;
    public float TurnBias;
}

[Serializable]
public struct ForcePoint : IBufferElementData
{
    public float Weight;
    public float3 Offset;
}

public class ProbyBuoyantComponentAuthoring : MonoBehaviour, IConvertGameObjectToEntity
{
    [Header("Forces")]
    [Tooltip("Override RB center of mass, in local space."), SerializeField]
    public Vector3 CenterOfMass;
    public ForcePoint[] ForcePoints;
    [Tooltip("Vertical offset for where engine force should be applied.")]
    public float ForceHeightOffset;
    public float ForceMultiplier;
    [Tooltip("Width dimension of boat. The larger this value, the more filtered/smooth the wave response will be.")]
    public float MinSpatialLength;
    [UnityEngine.Range(0, 1)]
    public float TurningHeel;

    [Header("Drag")]
    public float DragInWaterUp;
    public float DragInWaterRight;
    public float DragInWaterForward;
    
    [Header("Control")]
    public float EnginePower = 7;
    public float TurnPower = 0.5f;
    public bool PlayerControlled = true;
    
    [Tooltip("Used to automatically add throttle input")]
    public float EngineBias;
    [Tooltip("Used to automatically add turning input")]
    public float TurnBias;
    
    
    public void Convert(Entity entity, EntityManager dstManager, GameObjectConversionSystem conversionSystem)
    {
        dstManager.AddComponentData(entity, new ProbyBuoyantComponent()
        {
            CenterOfMass = CenterOfMass, 
            ForceHeightOffset = ForceHeightOffset, 
            ForceMultiplier = ForceMultiplier, 
            MinSpatialLength = MinSpatialLength, 
            TurningHeel = TurningHeel, 
            DragInWaterUp = DragInWaterUp, 
            DragInWaterRight = DragInWaterRight, 
            DragInWaterForward = DragInWaterForward, 
            EnginePower = EnginePower, 
            TurnPower = TurnPower, 
            EngineBias = EngineBias, 
            TurnBias = TurnBias
        });

        dstManager.AddBuffer<ForcePoint>(entity);
        var forcePointsBuffer = dstManager.GetBuffer<ForcePoint>(entity);
        
        for (int i = 0; i < ForcePoints.Length; i++)
        {
            forcePointsBuffer.Add(ForcePoints[i]);
        }
    }

    #if UNITY_EDITOR
    public void OnDrawGizmos()
    {
        Handles.zTest = CompareFunction.Disabled;
        var originalColor = Handles.color;
        Handles.color = Color.green;
        Handles.DrawWireCube(transform.TransformPoint(CenterOfMass), new Vector3(0.3f, 0.3f, 0.3f));
        
        foreach (var forcePoint in ForcePoints)
        {
            Handles.color = new Color(1f, 0, 0, forcePoint.Weight);
            // Gizmos.DrawSphere(transform.TransformPoint(forcePoint.Offset), 0.2f);
            Handles.DrawWireCube(transform.TransformPoint(forcePoint.Offset + new float3(0f, CenterOfMass.y, 0f)), new Vector3(0.2f, 0.2f, 0.2f));
        }

        Handles.color = originalColor;
        Handles.zTest = CompareFunction.Always;
    }
    #endif
}
