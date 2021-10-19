using System;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Vermetio.AI;

[Serializable]
public struct RoamActionComponent : IActionComponent
{
    public BlobAssetReference<ActionDef> ActionDef => _actionDef;
    private BlobAssetReference<ActionDef> _actionDef;
    
    public float CurrentScore { get; set; }
    
    public IActionComponent Initialize(BlobAssetReference<ActionDef> actionDef)
    {
        return new RoamActionComponent()
        {
            _actionDef = actionDef
        };
    }
}
