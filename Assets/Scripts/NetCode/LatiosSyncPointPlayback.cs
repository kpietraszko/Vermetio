/*using Latios;
using Latios.Systems;
using Unity.Entities;

[UpdateInGroup(typeof(InitializationSystemGroup), OrderFirst = true)]
[UpdateAfter(typeof(BeginInitializationEntityCommandBufferSystem))]
public class LatiosSyncPointPlayback : RootSuperSystem
{
    protected override void CreateSystems()
    {
        EnableSystemSorting = false;
        latiosWorld.syncPoint = GetOrCreateAndAddSystem<SyncPointPlaybackSystem>();
        GetOrCreateAndAddSystem(typeof(SceneManagerSystem));
    }
}*/