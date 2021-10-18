using Unity.Entities;

namespace Vermetio.AI
{
    public abstract class ActionBase : ExpandableGenericScriptableObject
    {
        public abstract ActionConsideration[] Considerations { get; }
        public abstract BlobAssetReference<ActionDef> ConvertToBlobAndAddActionComponent(EntityManager entityManager, Entity entity);
    }
}