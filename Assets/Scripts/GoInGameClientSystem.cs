using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.NetCode;
using Unity.Transforms;

[UpdateInGroup(typeof(ClientSimulationSystemGroup))]
public class GoInGameClientSystem : SystemBase
{

    protected override void OnCreate()
    {
    }

    protected override void OnUpdate()
    {
        var commandBuffer = new EntityCommandBuffer(Allocator.Temp);
        
        Entities.WithNone<NetworkStreamInGame>().ForEach((Entity ent, ref NetworkIdComponent id) =>
        {
            commandBuffer.AddComponent<NetworkStreamInGame>(ent);
            var req = commandBuffer.CreateEntity();
            commandBuffer.AddComponent<ConnectionSystem.GoInGameRequest>(req);
            commandBuffer.AddComponent(req, new SendRpcCommandRequestComponent { TargetConnection = ent });
        }).Run();
        
        commandBuffer.Playback(EntityManager);
    }
}
