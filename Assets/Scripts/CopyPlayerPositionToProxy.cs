using System.Collections;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Entities;
using Unity.NetCode;
using Unity.Transforms;
using UnityEngine;
using Vermetio;

public class CopyPlayerPositionToProxy : MonoBehaviour
{
    
    void Update()
    {
        var world = EntityHelpers.GetWorldWith<ClientSimulationSystemGroup>(World.All);
        if (world == null)
            return;
        
        // TODO: cache both queries, because creating them allocates 0.5 kB (might not matter) 

        var networkIdComponents = world.EntityManager.CreateEntityQuery(typeof(NetworkIdComponent))
            .ToComponentDataArray<NetworkIdComponent>(Allocator.Temp);

        if (networkIdComponents.Length != 1)
            return;

        var entities = world.EntityManager.CreateEntityQuery(typeof(Translation), typeof(GhostOwnerComponent))
            .ToEntityArray(Allocator.Temp);

        for (int i = 0; i < entities.Length; i++)
        {
            if (world.EntityManager.GetComponentData<GhostOwnerComponent>(entities[i]).NetworkId != networkIdComponents[0].Value) // if this isn't the current client's boat
                continue;

            var playerPosition = world.EntityManager.GetComponentData<Translation>(entities[i]).Value;
            var playerRotation = world.EntityManager.GetComponentData<Rotation>(entities[i]).Value;
            // Debug.Log($"{playerPosition}");
            transform.SetPositionAndRotation(playerPosition, playerRotation);
            return;
        }
    }
}
