using Latios;
using Latios.Systems;
using Unity.Entities;

[UpdateInGroup(typeof(InitializationSystemGroup))]
[UpdateAfter(typeof(Unity.Scenes.SceneSystemGroup))]
public class LatiosWorldSync : RootSuperSystem
{
    protected override void CreateSystems()
    {
        EnableSystemSorting = false;
        GetOrCreateAndAddSystem(typeof(LatiosWorldSyncGroup));
    }
}