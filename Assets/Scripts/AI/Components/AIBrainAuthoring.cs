using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using Vermetio.AI;

[DisallowMultipleComponent]
public class AIBrainAuthoring : MonoBehaviour, IConvertGameObjectToEntity
{

    public void Convert(Entity entity, EntityManager dstManager, GameObjectConversionSystem conversionSystem)
    {
        dstManager.AddComponent<AIBrainComponent>(entity);
        // var buffer = dstManager.AddBuffer<ConsiderationInputElement>(entity);
        // buffer.Length = (int) ConsiderationInputType.Count;
    }
}
