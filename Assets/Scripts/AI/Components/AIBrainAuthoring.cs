using System;
using System.Reflection;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using Vermetio.AI;

[DisallowMultipleComponent]
public class AIBrainAuthoring : MonoBehaviour, IConvertGameObjectToEntity
{
    [SerializeField] 
    private ActionBase[] _actions;

    public void Convert(Entity entity, EntityManager dstManager, GameObjectConversionSystem conversionSystem)
    {
        var firstActionDefRef = BlobAssetReference<ActionDef>.Null;

        foreach (var action in _actions)
        {
            var actionDefBlobRef = action.ConvertToBlobAndAddActionComponent(dstManager, entity);

            if (!firstActionDefRef.IsCreated)
                firstActionDefRef = actionDefBlobRef;

        }

        dstManager.AddComponentData(entity, new AIBrainComponent() { CurrentAction = firstActionDefRef });
        //action1.GetType().BaseType.GetGenericArguments()[0]
        
        ;
    }

    private void OnValidate()
    {
        // Debug.Log($"{_actions[0].GetType()}");
    }
}
