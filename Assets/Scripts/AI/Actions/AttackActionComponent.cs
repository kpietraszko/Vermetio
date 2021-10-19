using System;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Vermetio.AI;

[Serializable]
public struct AttackActionComponent : IActionComponent
{
    public BlobAssetReference<ActionDef> ActionDef { get; private set; }
    public float CurrentScore { get; set; }
    public Entity TargetEnemy;

    public IActionComponent Initialize(BlobAssetReference<ActionDef> actionDef)
    {
        return new AttackActionComponent()
        {
            ActionDef = actionDef
        };
    }
}
