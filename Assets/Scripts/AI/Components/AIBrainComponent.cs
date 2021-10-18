using System;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Vermetio.AI;

[Serializable]
public struct AIBrainComponent : IComponentData
{
    // Might not work, and limits to only 1 action at a time. Could be fine, could not be
    public BlobAssetReference<ActionDef> CurrentAction;
}
