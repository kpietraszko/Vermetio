using System.Collections;
using System.Collections.Generic;
using Unity.Entities;
using Unity.NetCode;
using UnityEngine;

public class CopyPlayerPositionToProxy : MonoBehaviour
{
    // Update is called once per frame
    void Update()
    {
        var world = GetWorldWith<ClientSimulationSystemGroup>(World.All);
        if (world == null)
            return;

        world.EntityManager.CreateEntityQuery(typeof(NetworkIdComponent)).ToComponentDataArray<NetworkIdComponent>()
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
