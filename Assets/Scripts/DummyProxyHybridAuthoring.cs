using System.Collections;
using System.Collections.Generic;
using Unity.Entities;
using UnityEngine;


public class DummyProxyHybridAuthoring : MonoBehaviour, IConvertGameObjectToEntity
{
    public void Convert(Entity entity, EntityManager dstManager, GameObjectConversionSystem conversionSystem)
    {
        // #if UNITY_CLIENT
        conversionSystem.AddHybridComponent(this);
        Debug.Log("Added dummy proxy hybrid component");
        // #endif
    }
}