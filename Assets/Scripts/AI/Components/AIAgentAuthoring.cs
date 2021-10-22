using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using Vermetio.AI;

[DisallowMultipleComponent]
public class AIAgentAuthoring : MonoBehaviour, IConvertGameObjectToEntity
{

    public void Convert(Entity entity, EntityManager dstManager, GameObjectConversionSystem conversionSystem)
    {
        dstManager.AddComponent<AIAgentComponent>(entity);
        dstManager.AddComponent<AIActionTargetComponent>(entity);
        // var buffer = dstManager.AddBuffer<ConsiderationInputElement>(entity);
        // buffer.Length = (int) ConsiderationInputType.Count;
    }
}
