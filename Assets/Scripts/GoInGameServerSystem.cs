using System;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.NetCode;
using Unity.Transforms;
using UnityEngine;

[UpdateInGroup(typeof(ServerSimulationSystemGroup))]
public class GoInGameServerSystem : SystemBase
{
    protected override void OnUpdate()
    {
        var ecb = World.GetOrCreateSystem<BeginSimulationEntityCommandBufferSystem>().CreateCommandBuffer();
        Entities.WithNone<SendRpcCommandRequestComponent>().ForEach(
            (Entity reqEnt, ref ConnectionSystem.GoInGameRequest req,
                ref ReceiveRpcCommandRequestComponent reqSrc) =>
            {
                ecb.AddComponent<NetworkStreamInGame>(reqSrc.SourceConnection);
                Debug.Log(String.Format("Server setting connection {0} to in game",
                    GetComponent<NetworkIdComponent>(reqSrc.SourceConnection).Value));
                ecb.DestroyEntity(reqEnt);
            }).Run();
        
    }
}