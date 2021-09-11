using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.NetCode;
using Unity.Scenes;
using Unity.Transforms;
using UnityEngine;

[UpdateInGroup(typeof(ClientSimulationSystemGroup))]
public class GoInGameClientSystem : SystemBase
{
    private SceneSystem _sceneSystem;

    protected override void OnCreate()
    {
        RequireSingletonForUpdate<GhostPrefabCollectionComponent>();
        RequireForUpdate(GetEntityQuery(ComponentType.ReadOnly<NetworkIdComponent>(), ComponentType.Exclude<NetworkStreamInGame>()));
        _sceneSystem = World.GetExistingSystem<SceneSystem>();
    }

    protected override void OnUpdate()
    {
        var ecb = World.GetOrCreateSystem<BeginSimulationEntityCommandBufferSystem>().CreateCommandBuffer();
        var scenes = GetEntityQuery(typeof(SceneReference), typeof(SubScene)).ToEntityArray(Allocator.Temp);
        for (int i = 0; i < scenes.Length; i++)
        {
            if (!_sceneSystem.IsSceneLoaded(scenes[i]))
                return;
        }

        Entities.WithoutBurst().WithNone<NetworkStreamInGame>().ForEach((Entity ent, ref NetworkIdComponent id) =>
        {
            // TODO: make sure that the subscene has finished loading before sending InGame, 

            
            ecb.AddComponent<NetworkStreamInGame>(ent);
            var req = ecb.CreateEntity();
            ecb.AddComponent<ConnectionSystem.GoInGameRequest>(req);
            ecb.AddComponent(req, new SendRpcCommandRequestComponent { TargetConnection = ent });
        }).Run();
       
    }
}
