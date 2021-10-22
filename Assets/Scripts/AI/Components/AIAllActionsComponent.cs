using System;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Vermetio.AI;

[Serializable]
public struct AIAllActionsComponent : IComponentData
{
    public int AllActionsCount;
    public BlobAssetReference<AIAllActionsBlob> AllActionsDefs;
}
