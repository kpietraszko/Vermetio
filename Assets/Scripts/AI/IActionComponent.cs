using System.Collections;
using System.Collections.Generic;
using Unity.Entities;
using UnityEngine;

namespace Vermetio.AI
{
    public interface IActionComponent : IComponentData
    {
        public BlobAssetReference<ActionDef> ActionDef { get; }
        public float CurrentScore { get; set; }

        public IActionComponent Initialize(BlobAssetReference<ActionDef> actionDef);
    }
}