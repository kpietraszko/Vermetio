using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using GenericUnityObjects;
using Unity.Collections;
using Unity.Entities;
using UnityEngine;

namespace Vermetio.AI
{
    [CreateGenericAssetMenu(FileName = "Action", MenuName = "AI/Action")]
    [Serializable]
    // ActionBase so that I can assign instance in MB's inspector.
    // IAction is the component the brain will have when this is the current action.
    public class Action<TAction> : ActionBase where TAction : struct, IActionComponent
    {
        [HideInInspector]
        public override ActionConsideration[] Considerations => _considerations;

        // [SerializeField]
        // private string ActionName;
        
        [SerializeField]
        public ActionConsideration[] _considerations;

        private TAction _dummyComponent = new TAction(); 

        public override void AddActionComponent(EntityManager entityManager, Entity entity, int actionId)
        {
            // var actionDefBlobAssetRef = builder.CreateBlobAssetReference<ActionDef>(Allocator.Persistent);
            var actionComponent = (TAction) _dummyComponent.Initialize(actionId, default);
            // Adding a specific component per action to then compare it to actions I'm iterating over and execute specific action's code
            entityManager.AddComponentData(entity, actionComponent);
            
        }
    }
}