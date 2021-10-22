using System.Collections;
using System.Collections.Generic;
using Unity.Entities;
using UnityEngine;

namespace Vermetio.AI
{
    public interface IActionComponent : IComponentData
    {
        public int ActionId { get; }
        public BlobAssetReference<ActionDef> ActionDef { get; }

        public IActionComponent Initialize(int actionId, BlobAssetReference<ActionDef> actionDef);
    }
}