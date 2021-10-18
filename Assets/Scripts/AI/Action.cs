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
        private ActionConsideration[] _considerations;

        private TAction _dummyComponent = new TAction(); 

        public override BlobAssetReference<ActionDef> ConvertToBlobAndAddActionComponent(EntityManager entityManager, Entity entity)
        {
            using var builder = new BlobBuilder(Allocator.Temp);
            ref var actionBlobAsset = ref builder.ConstructRoot<ActionDef>();
            actionBlobAsset.ActionName = new FixedString32(name);
            var considerations = builder.Allocate(ref actionBlobAsset.Considerations, _considerations.Length);

            for (int i = 0; i < _considerations.Length; i++)
            {
                var cons = _considerations[i];
                considerations[i] = new ConsiderationDef()
                {
                    ConsiderationName = new FixedString64(cons.name), 
                    InputType = cons.InputType,
                    Curve = new ConsiderationCurve()
                    {
                        CurveType = cons.CurveType,
                        B = cons.CurveB,
                        C = cons.CurveC,
                        K = cons.CurveK,
                        M = cons.CurveM
                    }
                };
            }
            
            var actionDefBlobAssetRef = builder.CreateBlobAssetReference<ActionDef>(Allocator.Persistent);
            var actionComponent = (TAction) _dummyComponent.Initialize(actionDefBlobAssetRef);
            entityManager.AddComponentData(entity, actionComponent);

            return actionDefBlobAssetRef;
        }
    }
}