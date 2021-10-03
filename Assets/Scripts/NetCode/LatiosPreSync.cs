using Latios;
using Latios.Systems;
using Unity.Entities;

[UpdateInGroup(typeof(InitializationSystemGroup), OrderFirst = true)]
[UpdateBefore(typeof(BeginInitializationEntityCommandBufferSystem))]
public class LatiosPreSync : RootSuperSystem
{
    protected override void CreateSystems()
    {
        EnableSystemSorting = false;
        GetOrCreateAndAddSystem(typeof(PreSyncPointGroup));
    }
}