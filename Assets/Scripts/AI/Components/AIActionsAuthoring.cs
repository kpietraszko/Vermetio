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
        using var builder = new BlobBuilder(Allocator.TempJob);
        ref var allActionsBlobAsset = ref builder.ConstructRoot<AIAllActionsBlob>();
        var actionsBlobArray = builder.Allocate(ref allActionsBlobAsset.AllActions, _actions.Length);
        for (var actionIdx = 0; actionIdx < _actions.Length; actionIdx++)
        {
            actionsBlobArray[actionIdx] = new ActionDef()
            {
                ActionName = new FixedString32(_actions[actionIdx].name)
            };
            
            var considerations = builder.Allocate(ref actionsBlobArray[actionIdx].Considerations, _actions[actionIdx].Considerations.Length);

            for (int consIdx = 0; consIdx < _actions[actionIdx].Considerations.Length; consIdx++)
            {
                var cons = _actions[actionIdx].Considerations[consIdx];
                considerations[consIdx] = new ConsiderationDef()
                {
                    // ConsiderationName = new FixedString64($"{this.name} - {cons.InputType}"), 
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
            
            _actions[actionIdx].AddActionComponent(dstManager, entity, actionIdx);
        }
        
        var allActionsBlobRef = builder.CreateBlobAssetReference<AIAllActionsBlob>(Allocator.Persistent);
        dstManager.AddComponentData(entity, new AIAllActionsComponent() { AllActionsCount = _actions.Length, AllActionsDefs = allActionsBlobRef});
        //action1.GetType().BaseType.GetGenericArguments()[0]
    }

    private void OnValidate()
    {
        // Debug.Log($"{_actions[0].GetType()}");
    }
}
