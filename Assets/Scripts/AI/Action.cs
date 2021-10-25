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
        [Header("M - slope", order = 0), Space(-10, order = 1), Header("K - exponent", order = 2), Space(-10, order = 3), Header("B - offset vertically", order=4), Space(-10, order=5), Header("C - offset horizontally", order=6)]
        private ActionConsideration[] _considerations;

        private TAction _dummyComponent = new TAction();

        // private void OnValidate()
        // {
        //     foreach (var consideration in _considerations)
        //     {
        //         consideration.GenerateCurvePreview();
        //     }
        // }

        public override void ConvertToBlobAndAddActionComponent(EntityManager entityManager, Entity entity, int actionId)
        {
            using var builder = new BlobBuilder(Allocator.TempJob);
            ref var actionBlobAsset = ref builder.ConstructRoot<ActionDef>();
            actionBlobAsset = new ActionDef()
            {
                ActionName = new FixedString32(name)
            };
            var considerations = builder.Allocate(ref actionBlobAsset.Considerations, _considerations.Length);

            for (int i = 0; i < _considerations.Length; i++)
            {
                var cons = _considerations[i];
                considerations[i] = new ConsiderationDef()
                {
                    // ConsiderationName = new FixedString64($"{this.name} - {cons.InputType}"), 
                    InputType = cons.InputType, 
                    Curve = new ConsiderationCurve()
                    {
                        CurveType = cons.CurveType, 
                        B = cons.B, 
                        C = cons.C, 
                        K = cons.K, 
                        M = cons.M
                    }
                };
            }
            
            var actionDefBlobAssetRef = builder.CreateBlobAssetReference<ActionDef>(Allocator.Persistent);
            // can't do new TAction because compiler gets confused for some reason
            var actionComponent = (TAction)_dummyComponent.Initialize(actionId, actionDefBlobAssetRef); 
            // Adding a specific component per action to then compare it to actions I'm iterating over and execute specific action's code
            entityManager.AddComponentData(entity, actionComponent);
        }

    }
}