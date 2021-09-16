using System.Collections;
using System.Collections.Generic;
using Unity.Entities;
using Unity.NetCode;
using Unity.Transforms;
using UnityEngine;

namespace Vermetio
{
    public static class EntityHelpers
    {
        public static Entity GetGhostPrefab<T>(EntityManager entityManager) where T : struct
        {
            var ghostCollection = entityManager.CreateEntityQuery(typeof(GhostPrefabCollectionComponent))
                .GetSingletonEntity();
            var prefabs = entityManager.GetBuffer<GhostPrefabBuffer>(ghostCollection);
            for (int ghostId = 0; ghostId < prefabs.Length; ++ghostId)
            {
                if (entityManager.HasComponent<T>(prefabs[ghostId].Value))
                    return prefabs[ghostId].Value;
            }

            return Entity.Null;
        }

        public static Entity GetRootParent(Entity entity, ComponentDataFromEntity<Parent> parentPerEntity)
        {
            var currentParent = entity;
            while (parentPerEntity.HasComponent(currentParent)) // while currentParent has a parent
            {
                currentParent = parentPerEntity[currentParent].Value;
            }

            return currentParent;
        }
        
        public static World GetWorldWith<T>(World.NoAllocReadOnlyCollection<World> worlds) where T : ComponentSystemBase
        {
            foreach (var world in worlds)
            {
                if (world.GetExistingSystem<T>() != null)
                    return world;
            }

            return null;
        }
    }
}
