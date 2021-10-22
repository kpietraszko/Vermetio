using Unity.Entities;
using Vermetio.AI;

namespace Vermetio.AI
{
    public struct AIAllActionsBlob
    { 
        public BlobArray<ActionDef> AllActions;
    }
}