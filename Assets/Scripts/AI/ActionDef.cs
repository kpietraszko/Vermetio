using System.ComponentModel;
using Unity.Collections;
using Unity.Entities;

namespace Vermetio.AI
{
    public struct ActionDef/*<TAction> where TAction : IActionComponent*/
    {
        public FixedString32 ActionName; // should only be used for debugging
        // [EditorBrowsable(EditorBrowsableState.Never)]
        // public TAction DummyComponent;
        public BlobArray<ConsiderationDef> Considerations;
    }
}