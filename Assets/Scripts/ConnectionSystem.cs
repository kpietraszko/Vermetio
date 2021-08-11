using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.NetCode;
using Unity.Networking.Transport;
using Unity.Transforms;
using UnityEngine;

[UpdateInWorld(UpdateInWorld.TargetWorld.Default)]
[AlwaysSynchronizeSystem]
public class ConnectionSystem : SystemBase
{
    // Singleton component to trigger connections once from a control system
    struct InitGameComponent : IComponentData
    {
    }
    
    public struct GoInGameRequest : IRpcCommand
    {
    }

    protected override void OnCreate()
    {
        RequireSingletonForUpdate<InitGameComponent>();
        // Create singleton, require singleton for update so system runs once
        EntityManager.CreateEntity(typeof(InitGameComponent));
    }

    protected override void OnUpdate()
    {
        // Destroy singleton to prevent system from running again
        EntityManager.DestroyEntity(GetSingletonEntity<InitGameComponent>());
        
        
        var serverWorld = GetWorldWith<ServerSimulationSystemGroup>(World.All);

        if (serverWorld != null)
        {
            #if UNITY_EDITOR || UNITY_SERVER
            var network = serverWorld.GetExistingSystem<NetworkStreamReceiveSystem>();
            var tickRate = serverWorld.EntityManager.CreateEntity();
            serverWorld.EntityManager.AddComponentData(tickRate, new ClientServerTickRate
                {
                    SimulationTickRate = 30,
                    NetworkTickRate = 30,
                    MaxSimulationStepsPerFrame = 4
                });

                // Server world automatically listens for connections from any host
                NetworkEndPoint ep = NetworkEndPoint.AnyIpv4;
                ep.Port = 7979;
                Debug.Log("Listening... DSGFDOSGMJOFIS");
                network.Listen(ep);
            #endif
        }

        var clientWorld = GetWorldWith<ClientSimulationSystemGroup>(World.All);
        if (clientWorld != null)
        {
            var network = clientWorld.GetExistingSystem<NetworkStreamReceiveSystem>();
            var tickRate = clientWorld.EntityManager.CreateEntity();
            clientWorld.EntityManager.AddComponentData(tickRate, new ClientServerTickRate
            {
                SimulationTickRate = 30,
                NetworkTickRate = 30, 
                MaxSimulationStepsPerFrame = 4
            });
                
            // Client worlds automatically connect to localhost
            NetworkEndPoint ep = NetworkEndPoint.LoopbackIpv4;
            ep.Port = 7979;
            Debug.Log("Connecting...");
            var entity = network.Connect(ep);
            #if UNITY_EDITOR
            EntityManager.SetName(entity, "Connection");
            #endif
        }
    }

    private World GetWorldWith<T>(World.NoAllocReadOnlyCollection<World> worlds) where T : ComponentSystemBase
    {
        foreach (var world in worlds)
        {
            if (world.GetExistingSystem<T>() != null)
                return world;
        }

        return null;
    }
}