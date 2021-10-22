using System;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Vermetio.AI;

[Serializable]
public struct AttackActionComponent : IActionComponent
{
    public int ActionId { get; private set; }
    public BlobAssetReference<ActionDef> ActionDef { get; private set; }

    public IActionComponent Initialize(int actionId, BlobAssetReference<ActionDef> actionDef)
    {
        return new AttackActionComponent()
        {
            ActionId = actionId, 
            // ActionDef = actionDef
        };
    }
}
