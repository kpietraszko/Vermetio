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
        var commandBuffer = new EntityCommandBuffer(Allocator.Temp);
        Entities.WithNone<SendRpcCommandRequestComponent>().ForEach(
            (Entity reqEnt, ref ConnectionSystem.GoInGameRequest req,
                ref ReceiveRpcCommandRequestComponent reqSrc) =>
            {
                commandBuffer.AddComponent<NetworkStreamInGame>(reqSrc.SourceConnection);
                Debug.Log(String.Format("Server setting connection {0} to in game",
                    GetComponent<NetworkIdComponent>(reqSrc.SourceConnection).Value));
                commandBuffer.DestroyEntity(reqEnt);
            }).Run();
        
        commandBuffer.Playback(EntityManager);
    }
}