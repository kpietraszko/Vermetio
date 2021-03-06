using Unity.Entities;

namespace Vermetio.AI
{
    public abstract class ActionBase : ExpandableGenericScriptableObject
    {
        public abstract ActionConsideration[] Considerations { get; }
        public abstract void ConvertToBlobAndAddActionComponent(EntityManager entityManager, Entity entity, int actionId);
    }
}