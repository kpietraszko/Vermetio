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
using Vermetio;

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

        var serverWorld = EntityHelpers.GetWorldWith<ServerSimulationSystemGroup>(World.All);

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
                
            // Client worlds automatically connect to localhost or ip passed through command line argument
            NetworkEndPoint ep = NetworkEndPoint.LoopbackIpv4;
            ep.Port = 7979;

            var ipFromArg = GetArg("-ip");
            if (ipFromArg != null)
                ep = NetworkEndPoint.Parse(ipFromArg, 7979);
                
            Debug.Log("Connecting...");
            var entity = network.Connect(ep);
            #if UNITY_EDITOR
            clientWorld.EntityManager.SetName(entity, "Connection");
            #endif
        }
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
    
    private static string GetArg(string name)
    {
        var args = System.Environment.GetCommandLineArgs();
        for (int i = 0; i < args.Length; i++)
        {
            if (args[i] == name && args.Length > i + 1)
            {
                return args[i + 1];
            }
        }
        return null;
    }
}