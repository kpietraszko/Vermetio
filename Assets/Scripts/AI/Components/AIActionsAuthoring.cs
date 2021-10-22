using System;
using System.Collections.Generic;
using System.Reflection;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using Vermetio.AI;

[DisallowMultipleComponent]
public class AIActionsAuthoring : MonoBehaviour, IConvertGameObjectToEntity
{
    [SerializeField] 
    private ActionBase[] _actions;

    public void Convert(Entity entity, EntityManager dstManager, GameObjectConversionSystem conversionSystem)
    {

        for (var i = 0; i < _actions.Length; i++)
        {
            var action = _actions[i];
            action.ConvertToBlobAndAddActionComponent(dstManager, entity, i);
        }

        dstManager.AddComponentData(entity, new AIAllActionsComponent() { AllActionsCount = _actions.Length });
        //action1.GetType().BaseType.GetGenericArguments()[0]
    }

    private void OnValidate()
    {
        // Debug.Log($"{_actions[0].GetType()}");
    }
}
