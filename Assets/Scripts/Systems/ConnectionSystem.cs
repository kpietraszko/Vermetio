using System.Collections.Generic;
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
        // if (IsClientButNotThin())
        //     return;
        
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
                    SimulationTickRate = 60,
                    NetworkTickRate = 60,
                    MaxSimulationStepsPerFrame = 1
                });

                // Server world automatically listens for connections from any host
                NetworkEndPoint ep = NetworkEndPoint.AnyIpv4;
                ep.Port = 7979;
                Debug.Log("Listening... DSGFDOSGMJOFIS");
                network.Listen(ep);
            #endif
        }

        var clientWorlds = GetWorldsWith<ClientSimulationSystemGroup>(World.All);
        foreach (var clientWorld in clientWorlds)
        {
            var network = clientWorld.GetExistingSystem<NetworkStreamReceiveSystem>();
            var tickRate = clientWorld.EntityManager.CreateEntity();
            clientWorld.EntityManager.AddComponentData(tickRate, new ClientServerTickRate
            {
                SimulationTickRate = 60,
                NetworkTickRate = 60, 
                MaxSimulationStepsPerFrame = 1
            });
                
            // Client worlds automatically connect to localhost
            NetworkEndPoint ep = NetworkEndPoint.Parse("10.147.18.239", 7979); // NetworkEndPoint.LoopbackIpv4;
            // ep.Port = 7979;
            Debug.Log("Connecting...");
            var entity = network.Connect(ep);
            #if UNITY_EDITOR
            clientWorld.EntityManager.SetName(entity, "Connection");
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

    private IEnumerable<World> GetWorldsWith<T>(World.NoAllocReadOnlyCollection<World> worlds)
        where T : ComponentSystemBase
    {
        foreach (var world in worlds)
        {
            if (world.GetExistingSystem<T>() != null)
                yield return world;
        }
    }
    
    private bool IsClientButNotThin()
    {
        return World.GetExistingSystem<ClientSimulationSystemGroup>() != null && !TryGetSingleton<ThinClientComponent>(out _);
    }
}