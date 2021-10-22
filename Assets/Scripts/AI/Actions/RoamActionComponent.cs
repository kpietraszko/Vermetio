using System;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using Vermetio.AI;

[Serializable]
public struct RoamActionComponent : IActionComponent
{
    public int ActionId { get; private set; }
    public BlobAssetReference<ActionDef> ActionDef { get; private set; }

    public IActionComponent Initialize(int actionId, BlobAssetReference<ActionDef> actionDef)
    {
        return new RoamActionComponent()
        {
            ActionId = actionId, 
            ActionDef = actionDef
        };
    }
}
