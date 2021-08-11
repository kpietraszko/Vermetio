using System;
using Cinemachine;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

public class VirtualCameraHybridAuthoring : MonoBehaviour, IConvertGameObjectToEntity
{
    public void Start()
    {
        CinemachineCore.sShowHiddenObjects = true;
    }

    public void Convert(Entity entity, EntityManager dstManager, GameObjectConversionSystem conversionSystem)
    {
        var virtualCamera = GetComponent<CinemachineVirtualCamera>();

        conversionSystem.AddHybridComponent(virtualCamera);
        Debug.Log("Added virtual camera to hybrid component");
        // virtualCamera.enabled = false;
    }

    [ContextMenu("Show hidden objects")]
    public void ShowHidden()
    {
        CinemachineCore.sShowHiddenObjects = true;
    }
}
