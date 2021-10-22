using System.ComponentModel;
using Unity.Collections;
using Unity.Entities;

namespace Vermetio.AI
{
    public struct AllActionsDefs/*<TAction> where TAction : IActionComponent*/
    {
        public BlobArray<BlobAssetReference<ActionDef>> ActionsDefs;
    }
}